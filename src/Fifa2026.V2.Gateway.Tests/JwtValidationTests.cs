using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// Story 2.3 AC-6 / AC-7 — Token VÁLIDO no gateway YARP: 200 + propagação do claim
/// `oid` como X-Entra-OID downstream, e anti-spoofing do header.
///
/// NOTA de teste: o rate limiter (AC-5) é fixed-window 5/min POR IP, e todos os
/// clients de teste compartilham o IP de loopback dentro de uma mesma instância de
/// app (um fixture por classe). Por isso os cenários de JWT são divididos em duas
/// classes (esta + <see cref="JwtRejectionTests"/>), cada uma com ≤ 5 POSTs, para
/// não esbarrar no rate limiter — sem enfraquecer nenhum AC.
/// </summary>
public sealed class JwtValidationTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public JwtValidationTests(GatewayTestFixture fixture) => _fixture = fixture;

    private void StubBackendAccepted()
    {
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));
    }

    private static readonly object Payload = new { matchId = 1, category = "VIP", userId = 1, quantity = 1 };

    private List<string> ForwardedOids() => _fixture.Backend.LogEntries
        .Where(e => e.RequestMessage.Path == "/api/v2/purchase"
                    && e.RequestMessage.Headers!.ContainsKey("X-Entra-OID"))
        .SelectMany(e => e.RequestMessage.Headers!["X-Entra-OID"])
        .ToList();

    [Fact]
    public async Task ValidToken_Returns200_And_Forwards_XEntraOid()
    {
        StubBackendAccepted();
        const string oid = "12121212-3434-5656-7878-909090909090";

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create(oid: oid));

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // AC-7 — backend recebeu X-Entra-OID com o oid do token. Filtramos pelo oid
        // único deste teste (LogEntries é compartilhado — não usar .Last()).
        Assert.Contains(oid, ForwardedOids());
    }

    [Fact]
    public async Task SpoofedXEntraOidHeader_FromClient_IsStripped()
    {
        StubBackendAccepted();
        const string realOid = "aaaaaaaa-1111-2222-3333-444444444444";

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create(oid: realOid));
        // Cliente tenta forjar X-Entra-OID — deve ser descartado e substituído pelo oid do token.
        client.DefaultRequestHeaders.Add("X-Entra-OID", "ffffffff-ffff-ffff-ffff-ffffffffffff");

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // O oid REAL (do token) foi encaminhado; o valor FORJADO pelo cliente nunca.
        var forwardedOids = ForwardedOids();
        Assert.Contains(realOid, forwardedOids);
        Assert.DoesNotContain("ffffffff-ffff-ffff-ffff-ffffffffffff", forwardedOids);
    }
}

/// <summary>
/// Story 2.3 AC-6 / AC-12 — cenários de REJEIÇÃO de JWT no gateway YARP. Cada teste
/// envia 1 POST; 4 testes (≤ 5/min) cabem na janela do rate limiter. Classe separada
/// de <see cref="JwtValidationTests"/> para isolar a contagem do rate limiter por app.
/// </summary>
public sealed class JwtRejectionTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public JwtRejectionTests(GatewayTestFixture fixture) => _fixture = fixture;

    private void StubBackendAccepted()
    {
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));
    }

    private static readonly object Payload = new { matchId = 1, category = "VIP", userId = 1, quantity = 1 };

    [Fact]
    public async Task NoToken_Returns401()
    {
        StubBackendAccepted();
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        StubBackendAccepted();
        var client = _fixture.CreateClient();
        // Expirado há 10min (ClockSkew = 0 no gateway → rejeita imediatamente).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.Create(expires: DateTime.UtcNow.AddMinutes(-10)));

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_Returns401()
    {
        StubBackendAccepted();
        var client = _fixture.CreateClient();
        // Issuer de OUTRO tenant — M-1: ValidIssuer explícito recusa (não 'common').
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.Create(
                issuer: "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0"));

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_Returns401()
    {
        StubBackendAccepted();
        var client = _fixture.CreateClient();
        // Audience que não casa com o clientId esperado (M-1: ValidAudiences explícito).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestTokenFactory.Create(audience: "api://some-other-app"));

        var response = await client.PostAsJsonAsync("/purchase", Payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
