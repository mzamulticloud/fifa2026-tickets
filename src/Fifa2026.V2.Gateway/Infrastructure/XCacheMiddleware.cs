using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Yarp.ReverseProxy.Model;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// AC-6 — Cache de borda (30s) com header <c>X-Cache: HIT/MISS</c>, paridade com
/// <c>cache-lookup</c>/<c>cache-store</c> do APIM (ADE-004 Inv 3).
///
/// POR QUE NÃO o OutputCache nativo do ASP.NET: o forwarder do YARP chama
/// <c>IHttpResponseBodyFeature.DisableBuffering()</c> no caminho da resposta (streaming),
/// o que impede o <c>OutputCacheStream</c> de capturar o corpo — a resposta proxied nunca
/// é armazenada (X-Cache eterno MISS). Somam-se a isso a DefaultPolicy conservadora (que
/// desabilita cache em requests com <c>Authorization</c>, exigido desde a F3) e diferenças
/// de features sob o TestServer. Em vez de lutar contra essas três camadas, capturamos a
/// resposta EXPLICITAMENTE aqui (cache em código — o espírito do AC-6).
///
/// ESCOPO: cacheia SOMENTE a rota YARP <c>purchase-get</c> (GET de status, keyed pelo path
/// = correlationId). NÃO cacheia POST nem as rotas de streaming (<c>/mcp</c>, <c>/llm</c>,
/// <c>/flow-events/hubs</c>) — cache de URL com corpos diferentes quebraria MCP/LLM/SignalR.
/// Mantém a posição de pipeline do design original (antes da autenticação): o status é
/// keyed pelo correlationId e não depende do usuário; só respostas 200 são armazenadas.
/// </summary>
public sealed class XCacheMiddleware
{
    private readonly RequestDelegate _next;

    private const string CacheHeader = "X-Cache";
    private const string CacheableRouteId = "purchase-get";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public XCacheMiddleware(RequestDelegate next) => _next = next;

    private sealed record CachedResponse(int StatusCode, string? ContentType, byte[] Body);

    public async Task InvokeAsync(HttpContext context, IMemoryCache cache)
    {
        var routeId = context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.RouteId;
        var isCacheable = HttpMethods.IsGet(context.Request.Method) && routeId == CacheableRouteId;

        if (!isCacheable)
        {
            // Rotas não-cacheáveis: garante o header default MISS sem tocar no corpo
            // (OnStarting roda quando os headers ainda são graváveis).
            context.Response.OnStarting(static state =>
            {
                var ctx = (HttpContext)state;
                if (!ctx.Response.Headers.ContainsKey(CacheHeader))
                {
                    ctx.Response.Headers[CacheHeader] = "MISS";
                }
                return Task.CompletedTask;
            }, context);

            await _next(context);
            return;
        }

        var cacheKey = "edge:" + context.Request.Path.Value + context.Request.QueryString.Value;

        // --- cache-lookup ---
        if (cache.TryGetValue(cacheKey, out CachedResponse? cached) && cached is not null)
        {
            context.Response.StatusCode = cached.StatusCode;
            if (cached.ContentType is not null)
            {
                context.Response.ContentType = cached.ContentType;
            }
            context.Response.Headers[CacheHeader] = "HIT";
            context.Response.ContentLength = cached.Body.Length;
            await context.Response.Body.WriteAsync(cached.Body);
            return; // short-circuit: NÃO chama o backend (paridade cache-lookup do APIM).
        }

        // --- MISS: executa o proxy capturando o corpo num buffer próprio ---
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        context.Response.Headers[CacheHeader] = "MISS";

        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var bytes = buffer.ToArray();

        // --- cache-store: só respostas 200 entram no store (30s) ---
        if (context.Response.StatusCode == StatusCodes.Status200OK)
        {
            cache.Set(cacheKey, new CachedResponse(context.Response.StatusCode, context.Response.ContentType, bytes), CacheDuration);
        }

        // Repassa o corpo capturado ao cliente.
        if (bytes.Length > 0)
        {
            context.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes);
        }
    }
}
