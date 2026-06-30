using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// AC-4 / AC-8 — Roteamento com path rewrite e injeção de X-Correlation-ID.
/// Valida que POST /purchase no gateway chega como POST /api/v2/purchase no backend
/// e que o gateway injeta o header X-Correlation-ID downstream.
///
/// Story 2.3 — com a validação de JWT ATIVADA (AC-6), as rotas v2 exigem Bearer
/// token Entra válido. Estes testes usam um token de teste válido (TestTokenFactory).
/// </summary>
public sealed class RoutingTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public RoutingTests(GatewayTestFixture fixture) => _fixture = fixture;

    private HttpClient AuthenticatedClient()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());
        return client;
    }

    [Fact]
    public async Task PostPurchase_IsRewrittenTo_ApiV2Purchase_OnBackend()
    {
        // Arrange — backend só responde 202 na rota /api/v2/purchase (path rewrite OK).
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"correlationId\":\"11111111-1111-1111-1111-111111111111\",\"status\":\"queued\"}"));

        var client = AuthenticatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/purchase",
            new { matchId = 1, category = "VIP", userId = 1, quantity = 1 });

        // Assert — encaminhado com sucesso (path rewrite /purchase → /api/v2/purchase).
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", body);
    }

    [Fact]
    public async Task Gateway_Injects_XCorrelationId_Downstream_WhenAbsent()
    {
        // Arrange — backend ecoa o header X-Correlation-ID que recebeu.
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));

        var client = AuthenticatedClient();

        // Act — cliente NÃO envia X-Correlation-ID; o gateway deve gerar um GUID.
        var response = await client.PostAsJsonAsync("/purchase",
            new { matchId = 1, category = "VIP", userId = 1, quantity = 1 });

        // Assert — gateway devolve X-Correlation-ID ao cliente (AC-8 / ADE-000 Inv 5).
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var correlationId = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.True(Guid.TryParse(correlationId, out _),
            "X-Correlation-ID devolvido deve ser um GUID válido.");

        // Backend recebeu o header injetado.
        var log = _fixture.Backend.LogEntries.Last();
        Assert.True(log.RequestMessage.Headers!.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
