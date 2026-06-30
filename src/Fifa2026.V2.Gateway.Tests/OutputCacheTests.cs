using System.Net;
using System.Net.Http.Headers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace Fifa2026.V2.Gateway.Tests;

/// <summary>
/// AC-6 — Output cache em código (paridade com APIM cache-lookup/cache-store).
/// 2 GETs idênticos: o 1º é MISS (vai ao backend), o 2º é HIT (servido do store,
/// header X-Cache: HIT, sem nova chamada ao backend).
/// </summary>
public sealed class OutputCacheTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public OutputCacheTests(GatewayTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SecondIdenticalGet_Returns_CacheHit_WithoutHittingBackend()
    {
        // Arrange — backend responde 200 na rota de status.
        var correlationId = "22222222-2222-2222-2222-222222222222";
        _fixture.Backend
            .Given(Request.Create()
                .WithPath($"/api/v2/purchase/{correlationId}")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"status\":\"completed\"}"));

        // Story 2.3 AC-6 — rota v2 exige Bearer válido (validação JWT ativada).
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokenFactory.Create());
        var path = $"/purchase/{correlationId}";

        // Act — 1ª chamada (MISS, popula o cache).
        var first = await client.GetAsync(path);
        // 2ª chamada idêntica (HIT, servida do store).
        var second = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Equal("MISS", first.Headers.GetValues("X-Cache").Single());
        Assert.Equal("HIT", second.Headers.GetValues("X-Cache").Single());

        // O backend só foi chamado UMA vez (a 2ª veio do cache).
        var backendCalls = _fixture.Backend.LogEntries
            .Count(e => e.RequestMessage.Path == $"/api/v2/purchase/{correlationId}");
        Assert.Equal(1, backendCalls);
    }
}
