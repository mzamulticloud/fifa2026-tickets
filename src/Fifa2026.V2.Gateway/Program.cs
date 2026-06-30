using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Fifa2026.V2.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy.Transforms;

// =============================================================================
// Fifa2026.V2.Gateway — Gateway profissional em código C# com YARP (Story 2.2 / F2)
//
// Substitui o APIM Developer (ADE-004): rate-limit, output cache, CORS, header
// transform e JWT placeholder são MECANISMOS DE CÓDIGO, não policies XML opacas.
// Cada capacidade tem paridade 1:1 com uma policy APIM (ADE-004 Invariante 3).
//
// Pipeline (ORDEM IMPORTA — ADE-004 / story Task 2.6):
//   UseCors → UseRateLimiter → XCacheMiddleware (cache 30s) → UseAuthentication
//           → UseAuthorization → MapReverseProxy
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// Constantes de configuração de pipeline.
const string RateLimiterPolicy = "fixed";              // partição fixed-window por IP (AC-5)
const string CorsPolicy = "frontend";                   // origin restrito ao front (AC-7)
const string CorrelationHeader = "X-Correlation-ID";    // ADE-000 Inv 5 / AC-8
const string EntraOidHeader = "X-Entra-OID";            // Story 2.3 AC-7 / ADE-005 Inv 4

// Claim names do Microsoft Identity Platform (AC-14 anti-hallucination — validados
// contra docs oficiais "id-token-claims-reference" / "access-token-claims-reference").
//   - "oid": object id estável do usuário no tenant (token v2.0 / endpoint /v2.0).
//   - URI longa: nome do mesmo claim após o mapeamento de inbound claims do
//     JwtBearer handler (System.Security.Claims) — usado como fallback (ADE-005 Inv 4 /
//     story troubleshooting "Claim oid ausente").
const string OidClaim = "oid";
const string OidClaimUri = "http://schemas.microsoft.com/identity/claims/objectidentifier";

// Quartas / "admin 100% workforce" — header de shared secret e id do cluster v1.
// O gateway prova ao backend Node/Express v1 que a request administrativa passou pelo
// guardião (e não é spoof). Lido de Gateway:AdminSharedSecret (App Setting
// Gateway__AdminSharedSecret; vazio no repo = injeção desligada, igual ao backend).
const string GatewayKeyHeader = "X-Gateway-Key";
const string BackendV1ClusterId = "backend-v1";
var adminSharedSecret = builder.Configuration["Gateway:AdminSharedSecret"];

// -----------------------------------------------------------------------------
// YARP reverse proxy (ADE-004 Inv 1 e 2): rotas/clusters do appsettings.json +
// transforms programáticos (X-Correlation-ID, que exige geração de GUID novo).
// O IProxyConfigFilter sobrescreve a destination do cluster com a URL real da
// Function F1 (env FunctionAppF1Url — ADE-003 Inv 3, nunca hardcoded). A
// connection string SQL permanece NAS FUNCTIONS, não aqui.
// -----------------------------------------------------------------------------
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddConfigFilter<FunctionDestinationConfigFilter>()
    // Story 2.5 / F5 — injeta a URL real do McpServer no cluster mcp-server (AC-8).
    .AddConfigFilter<McpServerDestinationConfigFilter>()
    // Story 2.6 / F6 — injeta a URL real do serviço FlowEvents no cluster flow-events
    // (AC-3/AC-5). O gateway permanece o NÓ ZERO: injeta X-Correlation-ID nas requests
    // ao FlowEvents também (mesmo transform global de borda).
    .AddConfigFilter<FlowEventsDestinationConfigFilter>()
    // Quartas / "admin 100% workforce" — injeta a URL real do backend Node/Express v1
    // no cluster backend-v1 (rotas /admin/* proxiadas com a policy AdminOnly).
    .AddConfigFilter<BackendV1DestinationConfigFilter>()
    .AddTransforms(transformBuilderContext =>
    {
        // AC-8 / ADE-000 Inv 5 — injeta X-Correlation-ID (novo GUID se ausente) em
        // CADA requisição encaminhada ao backend. Aplicado em TODAS as rotas
        // (gateway é o nó zero do Flow Visualizer de F6).
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var incoming = transformContext.HttpContext.Request.Headers[CorrelationHeader].ToString();
            var correlationId = string.IsNullOrWhiteSpace(incoming)
                ? Guid.NewGuid().ToString()
                : incoming;

            transformContext.ProxyRequest.Headers.Remove(CorrelationHeader);
            transformContext.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId);

            // Devolve o mesmo correlationId ao cliente (observabilidade de borda — AC-11).
            transformContext.HttpContext.Response.Headers[CorrelationHeader] = correlationId;

            return ValueTask.CompletedTask;
        });

        // Story 2.3 AC-7 / ADE-005 Inv 4 — propagação de identidade downstream.
        // Após o JWT ser validado pelo AddJwtBearer, extrai o claim `oid` do usuário
        // autenticado e o injeta como header X-Entra-OID na requisição encaminhada à
        // Function F1 (que grava entra_oid em SQL). A Function NUNCA valida o token —
        // confia no header propagado pelo gateway (guardião único de JWT).
        //
        // SEGURANÇA (defense-in-depth): SEMPRE remove qualquer X-Entra-OID que tenha
        // vindo do cliente ANTES de (eventualmente) injetar o valor derivado do token.
        // Isso impede spoofing de identidade — o cliente não consegue forjar o header.
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            // Anti-spoofing: descarta qualquer X-Entra-OID de origem externa.
            transformContext.ProxyRequest.Headers.Remove(EntraOidHeader);

            var user = transformContext.HttpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                // Token v2.0 traz o claim "oid"; após o mapeamento inbound do handler
                // o mesmo valor pode aparecer sob a URI longa (fallback — ADE-005 Inv 4).
                var oid = user.FindFirst(OidClaim)?.Value
                    ?? user.FindFirst(OidClaimUri)?.Value;

                if (!string.IsNullOrWhiteSpace(oid))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(EntraOidHeader, oid);
                }
            }

            // NÃO logamos o token nem o oid em texto (AC-12 / CodeRabbit focus area —
            // oid é PII de identidade; nunca aparece em log de aplicação).
            return ValueTask.CompletedTask;
        });

        // Quartas / "admin 100% workforce" — injeta X-Gateway-Key APENAS nas rotas do
        // cluster backend-v1 (/admin/*). O escopo é decidido em tempo de CONFIG: o
        // callback de transforms roda por rota, então só ANEXAMOS o transform quando a
        // rota aponta pro backend-v1. Assim o segredo NUNCA vaza para outros clusters
        // (functions-f1, mcp-server, flow-events) — nem é avaliado por request.
        if (string.Equals(
                transformBuilderContext.Cluster?.ClusterId,
                BackendV1ClusterId,
                StringComparison.OrdinalIgnoreCase))
        {
            transformBuilderContext.AddRequestTransform(transformContext =>
            {
                // Anti-spoofing (igual ao X-Entra-OID): SEMPRE descarta qualquer
                // X-Gateway-Key vindo do cliente antes de injetar o valor real.
                transformContext.ProxyRequest.Headers.Remove(GatewayKeyHeader);

                // Só injeta quando o segredo está configurado (vazio = injeção desligada,
                // backend cai no fluxo legado v1 — paridade com GATEWAY_SHARED_SECRET vazio).
                if (!string.IsNullOrEmpty(adminSharedSecret))
                {
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                        GatewayKeyHeader, adminSharedSecret);
                }

                return ValueTask.CompletedTask;
            });
        }
    });

// -----------------------------------------------------------------------------
// AC-5 — Rate limiting em código (paridade com APIM rate-limit-by-key).
// Fixed window por IP. UMA única policy "fixed" (aplicada em todas as rotas do proxy),
// mas o LIMITE é sensível ao PATH:
//   - rotas de cliente (ex.: /purchase): 5 req/min por IP (comportamento original — AC-5).
//   - rotas admin (/admin/*): 60 req/min por IP.
//
// DECISÃO (Quartas / "admin 100% workforce"): o Dashboard dispara várias chamadas
// (stats + sales, recarregamentos, paginação) e estouraria o limite apertado de 5/min
// (HTTP 429). Em vez de um 2º policy + per-route config no YARP (que duplicaria metadata
// de rate-limit junto à blanket RequireRateLimiting e tornaria a resolução ambígua),
// mantemos UMA policy com PARTIÇÕES SEPARADAS por path: "admin:{ip}" (60/min) e "{ip}"
// (5/min). Os contadores não se misturam, a rota /purchase continua 5/min (teste verde)
// e as rotas admin ganham folga sem afrouxar o resto do gateway.
// -----------------------------------------------------------------------------
const int ClientPermitLimit = 5;    // AC-5 original (rotas de cliente).
const int AdminPermitLimit = 60;    // rotas /admin/* (Dashboard faz N chamadas).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimiterPolicy, httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isAdmin = httpContext.Request.Path.StartsWithSegments("/admin");
        return RateLimitPartition.GetFixedWindowLimiter(
            // Partição namespaceada: admin e cliente nunca compartilham contador.
            partitionKey: isAdmin ? $"admin:{ip}" : ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = isAdmin ? AdminPermitLimit : ClientPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// -----------------------------------------------------------------------------
// AC-6 — Cache de borda (30s) em código (paridade com APIM cache-lookup/cache-store).
// Implementado pelo XCacheMiddleware (IMemoryCache) — NÃO pelo OutputCache nativo, que
// não captura respostas proxied pelo YARP (o forwarder chama DisableBuffering). Ver a
// documentação no próprio XCacheMiddleware.
// -----------------------------------------------------------------------------
builder.Services.AddMemoryCache();

// -----------------------------------------------------------------------------
// AC-7 — CORS restrito ao domínio do frontend (paridade com APIM cors).
// -----------------------------------------------------------------------------
var frontendOrigin = builder.Configuration["Gateway:FrontendOrigin"]
    ?? "https://fifa2026-web.azurewebsites.net";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(frontendOrigin)
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// =============================================================================
// Story 2.11 / Quartas — IDENTIDADE DOIS-MUNDOS: cliente CIAM + admin workforce
// (ADE-007 Inv 1/2/5/7, SUPERSEDE ADE-005). O gateway YARP valida DOIS issuers de
// forma issuer-agnóstica (ADE-004 Inv 4 preservada): o cliente final pelo Microsoft
// Entra External ID (CIAM, ciamlogin.com) e o admin/operador pelo Entra ID workforce
// (login.microsoftonline.com).
//
// ⚠️ NÃO é "só trocar a string" da authority. Para aceitar DOIS issuers num mesmo
// pipeline, registramos DOIS handlers JwtBearer concretos ("Ciam" e "Admin") e um
// PolicyScheme "Selector" que, a cada request, inspeciona o issuer NÃO-validado do
// bearer e ENCAMINHA (ForwardDefaultSelector) ao handler concreto correto. O handler
// escolhido então valida iss/aud/assinatura/lifetime do jeito normal (fail-closed).
//
// Por que selector por issuer (e não dois schemes "tentados em sequência")?
//   Encadear schemes faria o 1º handler logar erro de validação para todo token do
//   2º issuer (ruído + risco de challenge ambíguo). O selector roteia 1:1 pelo issuer,
//   então cada token é validado pelo handler do SEU mundo — limpo e determinístico.
//
// CARRY-FORWARD M-1 (gate S2.2) — FAIL-CLOSED em AMBOS os mundos: nenhum tenant tem
// default "common" (aceitaria tokens de qualquer tenant). Tenant E client são
// configuração OBRIGATÓRIA; ausência → a app não sobe. iss/aud validados
// EXPLICITAMENTE (ValidIssuer/ValidAudiences), não só inferidos do Authority.
//
// Config requerida (App Settings do Container App, sem valores reais no repo):
//   Jwt:CiamTenantId  — GUID do tenant CIAM (Entra External ID)
//   Jwt:CiamClientId  — Application (client) ID da App Reg SPA no CIAM (= aud do token cliente)
//   Jwt:CiamAuthority — (opcional) authority CIAM completa override; default derivado:
//                       https://<CiamTenantId>.ciamlogin.com/<CiamTenantId>/v2.0
//   Jwt:AdminTenantId — GUID do tenant workforce (admin)
//   Jwt:AdminClientId — Application (client) ID da App Reg admin (= aud do token admin)
// =============================================================================
const string CiamScheme = "Ciam";    // cliente final (Entra External ID / CIAM)
const string AdminScheme = "Admin";  // admin/operador (Entra ID workforce + App Roles)
const string SelectorScheme = "Selector"; // PolicyScheme que roteia pelo issuer

// --- Config CIAM (cliente) — fail-closed ---
var ciamTenantId = builder.Configuration["Jwt:CiamTenantId"];
var ciamClientId = builder.Configuration["Jwt:CiamClientId"];
var ciamAuthorityOverride = builder.Configuration["Jwt:CiamAuthority"];

if (string.IsNullOrWhiteSpace(ciamTenantId) ||
    string.Equals(ciamTenantId, "common", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Configuração de identidade do CLIENTE ausente/insegura: defina 'Jwt:CiamTenantId' " +
        "com o GUID do tenant CIAM (Entra External ID; não use 'common'). " +
        "Story 2.11 AC-5 / ADE-007 Inv 1.");
}

if (string.IsNullOrWhiteSpace(ciamClientId))
{
    throw new InvalidOperationException(
        "Configuração de identidade do CLIENTE ausente: defina 'Jwt:CiamClientId' com o " +
        "Application (client) ID da App Registration SPA no tenant CIAM (= audience do " +
        "access token do cliente). Story 2.11 AC-5.");
}

// Authority CIAM v2.0. Para Entra External ID o host é <tenant>.ciamlogin.com (NÃO
// login.microsoftonline.com — esse é o erro clássico das Quartas, ADE-007 Consequências).
// Discovery: https://<tenant>.ciamlogin.com/<tenantId>/v2.0/.well-known/openid-configuration
// (validado contra docs Microsoft Entra External ID — AC-19). O issuer emitido pelo
// CIAM em tokens v2.0 tem a forma https://<tenantId>.ciamlogin.com/<tenantId>/v2.0.
// Permitimos override completo via Jwt:CiamAuthority quando o tenant do instrutor
// usar um subdomínio nomeado (ex.: contoso.ciamlogin.com) em vez do GUID.
var ciamAuthority = !string.IsNullOrWhiteSpace(ciamAuthorityOverride)
    ? ciamAuthorityOverride
    : $"https://{ciamTenantId}.ciamlogin.com/{ciamTenantId}/v2.0";
var ciamIssuerV2 = ciamAuthority.TrimEnd('/');

// --- Config Admin (workforce) — fail-closed ---
var adminTenantId = builder.Configuration["Jwt:AdminTenantId"];
var adminClientId = builder.Configuration["Jwt:AdminClientId"];

if (string.IsNullOrWhiteSpace(adminTenantId) ||
    string.Equals(adminTenantId, "common", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Configuração de identidade do ADMIN ausente/insegura: defina 'Jwt:AdminTenantId' " +
        "com o GUID do tenant workforce do admin (não use 'common'). " +
        "Story 2.11 AC-12/AC-13 / ADE-007 Inv 5.");
}

if (string.IsNullOrWhiteSpace(adminClientId))
{
    throw new InvalidOperationException(
        "Configuração de identidade do ADMIN ausente: defina 'Jwt:AdminClientId' com o " +
        "Application (client) ID da App Registration admin (= audience do token admin). " +
        "Story 2.11 AC-12.");
}

var adminAuthority = $"https://login.microsoftonline.com/{adminTenantId}/v2.0";
var adminIssuerV2 = $"https://login.microsoftonline.com/{adminTenantId}/v2.0";

// Hosts de issuer usados pelo selector para rotear o token ao handler do seu mundo.
const string CiamIssuerHost = "ciamlogin.com";
const string WorkforceIssuerHost = "login.microsoftonline.com";

builder.Services
    // O DEFAULT é o PolicyScheme "Selector": toda autenticação passa por ele, que
    // decide (por issuer) qual handler concreto vai validar o token de fato.
    .AddAuthentication(SelectorScheme)
    .AddJwtBearer(CiamScheme, options =>
    {
        // CLIENTE — discovery do CIAM (ciamlogin.com). JWKS validam a assinatura RS256;
        // iss/aud/lifetime EXPLÍCITOS abaixo (fail-closed, não confia só no metadata).
        options.Authority = ciamAuthority;
        options.Audience = ciamClientId;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ciamIssuerV2,
            ValidateAudience = true,
            // aud do CIAM pode ser o client id ou o App ID URI (api://<client-id>).
            ValidAudiences = new[] { ciamClientId, $"api://{ciamClientId}" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Sem tolerância de relógio: token expirado → 401 (AC-6).
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer(AdminScheme, options =>
    {
        // ADMIN — discovery do workforce (login.microsoftonline.com). Mesma mecânica de
        // validação (ADE-004 issuer-agnóstico), só muda a authority/issuer/aud.
        options.Authority = adminAuthority;
        options.Audience = adminClientId;
        options.RequireHttpsMetadata = true;
        // Mantém os nomes de claim originais do token (não renomeia "roles" para a URI
        // longa do WS-Federation). Combinado com RoleClaimType="roles" abaixo, faz o
        // RequireRole("Admin") casar a App Role emitida pelo Entra na claim "roles".
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = adminIssuerV2,
            ValidateAudience = true,
            ValidAudiences = new[] { adminClientId, $"api://{adminClientId}" },
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // O Entra emite App Roles na claim "roles" (id-token-claims-reference). Mapeia
            // "roles" como o role claim type para que RequireRole("Admin") seja satisfeito.
            RoleClaimType = "roles",
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddPolicyScheme(SelectorScheme, "Bearer issuer selector", options =>
    {
        // Para CADA request: lê o issuer NÃO-validado do bearer e encaminha ao handler
        // concreto do mundo correto. A validação real (assinatura/iss/aud/lifetime) é
        // feita pelo handler escolhido — o selector NÃO confia no issuer lido aqui, só
        // o usa para ROTEAR. Se nada casar, default = caminho do cliente (CIAM).
        options.ForwardDefaultSelector = httpContext =>
        {
            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var issuer = handler.ReadJwtToken(token).Issuer ?? string.Empty;
                    if (issuer.Contains(CiamIssuerHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return CiamScheme;
                    }
                    if (issuer.Contains(WorkforceIssuerHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return AdminScheme;
                    }
                }
            }

            // Default: caminho do cliente (CIAM). Token ausente/ilegível cai aqui e o
            // handler CIAM responde 401 (fail-closed — nenhuma rota fica anônima).
            return CiamScheme;
        };
    });

// Autorização: a policy default exige usuário autenticado por QUALQUER um dos dois
// handlers concretos (o selector já roteou). Uma rota administrativa separada usa a
// policy "AdminOnly", que exige o esquema Admin E a claim de role "Admin" (App Role
// construída hands-on no Bloco 3 — ADE-007 Inv 5; decisão do owner: única role "Admin").
const string AdminRole = "Admin";
const string AdminOnlyPolicy = "AdminOnly";
builder.Services.AddAuthorization(options =>
{
    // Default (rotas v2 do cliente): basta estar autenticado (CIAM ou Admin).
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
            CiamScheme, AdminScheme)
        .RequireAuthenticatedUser()
        .Build();

    // AdminOnly: rota administrativa. Exige usuário autenticado E a App Role "Admin".
    // NÃO fixamos o esquema aqui de propósito: deixamos o PolicyScheme selector
    // autenticar (CIAM→Ciam, workforce→Admin). Assim:
    //   - token workforce com role "Admin"  → autenticado + role presente → 200;
    //   - token CIAM (cliente) válido        → AUTENTICADO mas sem a role → 403 (não 401);
    //   - sem token / token inválido         → não autenticado → 401.
    // Só o esquema Admin (workforce) carrega a claim "roles"; um cliente CIAM nunca a
    // tem, então a separação dos dois mundos é preservada (a App Role só existe no
    // workforce — ADE-007 Inv 5). Fixar AddAuthenticationSchemes(Admin) daria 401 (e
    // não 403) ao cliente CIAM, perdendo a distinção autenticado-mas-não-autorizado.
    options.AddPolicy(AdminOnlyPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(AdminRole));
});

// Observabilidade de borda (AC-11 / ADE-000 Inv 5) — App Insights se a connection
// string estiver presente (APPLICATIONINSIGHTS_CONNECTION_STRING). No-op sem ela.
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Pipeline na ORDEM correta (Task 2.6 / ADE-004):
app.UseCors(CorsPolicy);          // 1. CORS
app.UseRateLimiter();             // 2. Rate limiter (429)
app.UseMiddleware<XCacheMiddleware>(); // 3. Cache de borda (30s) + X-Cache HIT/MISS (AC-6)
app.UseAuthentication();          // 4. Authentication (selector roteia CIAM vs Admin)
app.UseAuthorization();           // 5. Authorization

// Endpoint de saúde para smoke test / Container App health probe.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway-yarp" }));

// Story 2.11 / Quartas (AC-12/AC-13) — rota administrativa demonstrativa protegida pela
// policy "AdminOnly" (esquema Admin/workforce + App Role "Admin"). Mostra a separação
// dos dois mundos NO PRÓPRIO gateway: um token CIAM (cliente), mesmo válido, é roteado
// pelo selector ao esquema Ciam e NÃO satisfaz AdminOnly → 403. Só um token workforce
// com role "Admin" passa. As rotas de cliente (proxy abaixo) seguem na DefaultPolicy
// (qualquer um dos dois esquemas autenticado). Em produção, rotas admin reais do proxy
// usariam .RequireAuthorization(AdminOnlyPolicy) no cluster correspondente.
app.MapGet("/admin/ping", () => Results.Ok(new { status = "ok", scope = "admin" }))
    .RequireAuthorization(AdminOnlyPolicy);

// 6. MapReverseProxy com rate-limit em todas as rotas, cache na rota GET e EXIGÊNCIA
//    de JWT válido (CIAM OU workforce — DefaultPolicy) em todas as rotas v2.
//    Sem Bearer válido → 401 (UseAuthentication/UseAuthorization rejeitam antes do
//    proxy). Token expirado/issuer errado/aud errado → 401 (AC-6). O selector escolhe
//    o handler do issuer do token (issuer-agnóstico — ADE-004 Inv 4 / ADE-007 Inv 2).
app.MapReverseProxy()
    .RequireRateLimiting(RateLimiterPolicy)
    .RequireAuthorization();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração (Task de testes).
public partial class Program { }
