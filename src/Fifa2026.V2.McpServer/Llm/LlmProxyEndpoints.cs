using System.Net.Http.Headers;

namespace Fifa2026.V2.McpServer.Llm;

/// <summary>
/// Story 2.5 / F5 — PROXY de LLM mínimo, server-side (DECISÃO DE SEGURANÇA da
/// chave da LLM). A API key NUNCA vai no bundle do browser; o frontend chama estes
/// endpoints (via gateway YARP, Bearer Entra), e ESTE proxy injeta a key (lida de
/// App Setting) e encaminha ao endpoint OFICIAL pinado de cada provider (ADE-002
/// Inv 3 — anti-hallucination AC-15).
///
/// Rotas (espelham src/lib/llm/proxy.ts do front):
///   POST /llm/gemini/{*path}   → https://generativelanguage.googleapis.com/v1beta/{path}?key=KEY
///   POST /llm/groq/{*path}     → https://api.groq.com/openai/v1/{path}  (Bearer KEY)
///   POST /llm/mistral/{*path}  → https://api.mistral.ai/v1/{path}       (Bearer KEY)
///
/// A key de cada provider vem de App Settings (NUNCA hardcoded):
///   GEMINI_API_KEY, GROQ_API_KEY, MISTRAL_API_KEY.
/// Ausência da key → 503 (proxy desabilitado para aquele provider), nunca vaza nada.
/// </summary>
public static class LlmProxyEndpoints
{
    private const string GeminiBase = "https://generativelanguage.googleapis.com/v1beta";
    private const string GroqBase = "https://api.groq.com/openai/v1";
    private const string MistralBase = "https://api.mistral.ai/v1";

    public static void MapLlmProxy(this WebApplication app)
    {
        app.MapPost("/llm/{provider}/{*path}", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        string provider,
        string path,
        HttpRequest request,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LlmProxyMarker> logger,
        CancellationToken cancellationToken)
    {
        var (baseUrl, keySetting, scheme) = provider.ToLowerInvariant() switch
        {
            "gemini" => (GeminiBase, "GEMINI_API_KEY", "query"),
            "groq" => (GroqBase, "GROQ_API_KEY", "bearer"),
            "mistral" => (MistralBase, "MISTRAL_API_KEY", "bearer"),
            _ => (string.Empty, string.Empty, string.Empty),
        };

        if (string.IsNullOrEmpty(baseUrl))
        {
            return Results.BadRequest(new { error = $"Provider LLM desconhecido: {provider}." });
        }

        var apiKey = configuration[keySetting];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Proxy LLM: {Setting} não configurado — provider {Provider} indisponível.", keySetting, provider);
            return Results.Json(
                new { error = $"{keySetting} não configurado no servidor. Configure como App Setting." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // Lê o corpo cru (o front já montou no formato do provider) e encaminha.
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var targetUrl = scheme == "query"
            // Gemini: key em query string (?key=...) — doc oficial.
            ? $"{baseUrl}/{path}?key={Uri.EscapeDataString(apiKey)}"
            : $"{baseUrl}/{path}";

        using var upstream = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

        if (scheme == "bearer")
        {
            // Groq/Mistral (OpenAI-compat): Authorization: Bearer <KEY>.
            upstream.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var client = httpClientFactory.CreateClient("llm");
        using var providerResponse = await client.SendAsync(upstream, cancellationToken);

        var responseBody = await providerResponse.Content.ReadAsStringAsync(cancellationToken);

        // Repassa status + corpo ao front. A KEY nunca aparece na resposta nem no log.
        return Results.Content(
            responseBody,
            contentType: "application/json",
            statusCode: (int)providerResponse.StatusCode);
    }

    /// <summary>Marcador de categoria para o ILogger do proxy.</summary>
    public sealed class LlmProxyMarker { }
}
