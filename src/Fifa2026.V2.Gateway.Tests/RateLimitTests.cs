using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// AC-5 — Rate limiting em código (paridade com APIM rate-limit-by-key).
/// Fixed window 5/min por IP: a 6ª chamada dentro da janela retorna HTTP 429.
/// </summary>
public sealed class RateLimitTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public RateLimitTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SixthRequest_WithinWindow_Returns_429()
    {
        // Arrange — backend sempre responde 202 (queremos isolar o rate-limiter).
        _fixture.Backend
            .Given(Request.Create().WithPath("/api/v2/purchase").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithBody("{\"correlationId\":\"x\",\"status\":\"queued\"}"));

        // Story 2.3 AC-6 — rota v2 exige Bearer válido (validação JWT ativada).
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());
        var payload = new { matchId = 1, category = "VIP", userId = 1, quantity = 1 };

        // Act — 5 chamadas dentro do limite, todas devem passar.
        for (var i = 1; i <= 5; i++)
        {
            var ok = await client.PostAsJsonAsync("/purchase", payload);
            Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
        }

        // 6ª chamada na mesma janela: bloqueada pelo rate-limiter.
        var blocked = await client.PostAsJsonAsync("/purchase", payload);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
