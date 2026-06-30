using System.Net;
using System.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Quartas / "admin 100% workforce" (MVP) — rotas administrativas PROXIADAS pro backend
/// v1 (cluster backend-v1) com a policy AdminOnly. Diferente do /admin/ping (minimal API
/// no próprio gateway), estas rotas atravessam o YARP e exercitam:
///   - autorização AdminOnly no proxy (200 admin / 403 cliente / 401 sem token);
///   - path rewrite /admin/stats → /api/admin/stats;
///   - injeção do header X-Gateway-Key (shared secret) ESCOPADA ao cluster backend-v1;
///   - anti-spoofing: X-Gateway-Key vindo do cliente é descartado e sobrescrito.
///
/// Rate limiter: as rotas /admin/* usam a partição admin (60/min), então o punhado de
/// chamadas desta classe não esbarra no limite (a partição de cliente segue 5/min).
/// </summary>
public sealed class AdminProxyTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public AdminProxyTests(GatewayTestFixture fixture) => _fixture = fixture;

    private void StubStats()
    {
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/admin/stats").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"stats\":{\"total_users\":1}}"));
    }

    private List<string> ForwardedGatewayKeys() => _fixture.Backend.LogEntries
        .Where(e => e.RequestMessage.Path == "/api/admin/stats"
                    && e.RequestMessage.Headers!.ContainsKey("X-Gateway-Key"))
        .SelectMany(e => e.RequestMessage.Headers!["X-Gateway-Key"])
        .ToList();

    [Fact]
    public async Task AdminToken_OnAdminStats_Returns200_AndInjects_GatewayKey_And_Oid()
    {
        // Token workforce com role "Admin" satisfaz AdminOnly → 200; o gateway injeta
        // X-Gateway-Key (shared secret) e X-Entra-OID downstream.
        StubStats();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.CreateAdmin());

        var response = await client.GetAsync("/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(GatewayTestFixture.AdminSharedSecret, ForwardedGatewayKeys());

        var log = _fixture.Backend.LogEntries.Last(
            e => e.RequestMessage.Path == "/api/admin/stats");
        Assert.True(log.RequestMessage.Headers!.ContainsKey("X-Entra-OID"));
    }

    [Fact]
    public async Task SpoofedGatewayKey_FromClient_IsOverwritten()
    {
        // Anti-spoofing: cliente envia um X-Gateway-Key forjado; o gateway o REMOVE e
        // injeta o segredo real — o backend nunca vê o valor do cliente.
        StubStats();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.CreateAdmin());
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Gateway-Key", "forged-evil-key");

        var response = await client.GetAsync("/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var keys = ForwardedGatewayKeys();
        Assert.Contains(GatewayTestFixture.AdminSharedSecret, keys);
        Assert.DoesNotContain("forged-evil-key", keys);
    }

    [Fact]
    public async Task CiamToken_OnAdminStats_Returns403()
    {
        // Token de CLIENTE (CIAM) válido é autenticado mas não tem a App Role "Admin"
        // → 403 (separação dos dois mundos preservada na rota proxiada).
        StubStats();
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());

        var response = await client.GetAsync("/admin/stats");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_OnAdminStats_Returns401()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/admin/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
