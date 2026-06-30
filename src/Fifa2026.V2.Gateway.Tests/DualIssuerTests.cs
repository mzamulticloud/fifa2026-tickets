using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 2.11 / Quartas (AC-5/AC-12/AC-13) — IDENTIDADE DOIS MUNDOS no gateway YARP.
/// O PolicyScheme "Selector" do Program.cs inspeciona o issuer não-validado do bearer
/// e roteia cada token ao handler concreto do seu mundo (Ciam=cliente, Admin=workforce).
///
/// Cobertura:
///   - Token CIAM (cliente) válido → 200 na rota de cliente + X-Entra-OID propagado.
///   - Token Admin (workforce) com role "Admin" → 200 na rota AdminOnly.
///   - Token CIAM válido na rota AdminOnly → 403 (cliente não é admin).
///   - Token Admin SEM a role "Admin" na rota AdminOnly → 403.
///
/// NOTA de rate limiter (herdada de JwtValidationTests): fixed-window 5/min POR IP,
/// compartilhado por todos os clients de teste numa mesma instância de app. Esta classe
/// faz ≤ 5 requisições para não esbarrar no limiter — sem enfraquecer nenhum AC.
/// </summary>
public sealed class DualIssuerTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public DualIssuerTests(GatewayTestFixture fixture) => _fixture = fixture;

    private void StubBackendAccepted()
    {
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));
    }

    private static readonly object Payload =
        new { matchId = 1, category = "VIP", userId = 1, quantity = 1 };

    private List<string> ForwardedOids() => _fixture.Backend.LogEntries
        .Where(e => e.RequestMessage.Path == "/api/v2/purchase"
                    && e.RequestMessage.Headers!.ContainsKey("X-Entra-OID"))
        .SelectMany(e => e.RequestMessage.Headers!["X-Entra-OID"])
        .ToList();

    [Fact]
    public async Task CiamToken_OnClientRoute_Returns200_And_Forwards_XEntraOid()
    {
        // AC-5/AC-11 — token do cliente (issuer CIAM) é roteado ao handler Ciam, valida
        // e propaga o oid como X-Entra-OID downstream (pipeline issuer-agnóstico).
        StubBackendAccepted();
        const string oid = "cccccccc-1111-2222-3333-444444444444";

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create(oid: oid));

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(oid, ForwardedOids());
    }

    [Fact]
    public async Task AdminToken_WithAdminRole_OnAdminRoute_Returns200()
    {
        // AC-12/AC-13 — token de admin (issuer workforce) com role "Admin" é roteado ao
        // handler Admin e satisfaz a policy AdminOnly → 200.
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.CreateAdmin());

        var response = await client.GetAsync("/admin/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CiamToken_OnAdminRoute_Returns403()
    {
        // AC-12/AC-13 — um token de CLIENTE (CIAM) válido é autenticado (não é 401), mas
        // a policy AdminOnly exige o esquema Admin + role "Admin"; o cliente não é admin
        // → 403 Forbidden (separação dos dois mundos NO gateway).
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());

        var response = await client.GetAsync("/admin/ping");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminToken_WithoutAdminRole_OnAdminRoute_Returns403()
    {
        // AC-12 — admin autenticado pelo workforce mas SEM a App Role "Admin" não passa
        // na policy AdminOnly → 403 (a role é exigida, não só o esquema).
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokenFactory.CreateAdmin(includeAdminRole: false));

        var response = await client.GetAsync("/admin/ping");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

/// <summary>
/// Story 2.11 (AC-6) — cenários de REJEIÇÃO 401 do mundo ADMIN (issuer workforce),
/// separados da classe acima para isolar a contagem do rate limiter por app (≤ 5 POSTs
/// não há aqui — são GETs em /admin/ping, fora do rate-limit do proxy, mas mantém-se a
/// separação de classe por clareza). Garante que o esquema Admin também é fail-closed.
/// </summary>
public sealed class AdminRejectionTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public AdminRejectionTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task NoToken_OnAdminRoute_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/admin/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredAdminToken_OnAdminRoute_Returns401()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokenFactory.CreateAdmin(expires: DateTime.UtcNow.AddMinutes(-10)));

        var response = await client.GetAsync("/admin/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
