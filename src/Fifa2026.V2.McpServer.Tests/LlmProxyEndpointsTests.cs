using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Story 2.5 / F5 — testes do proxy de LLM (decisão de segurança da key).
/// Confirma o comportamento FAIL-SAFE: sem key configurada (estado default do repo),
/// o proxy responde 503 (provider indisponível) — NUNCA vaza nada nem tenta
/// embutir a key. Provider desconhecido → 400. A key real não é exercida em teste.
/// </summary>
public sealed class LlmProxyEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LlmProxyEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unknown_provider_returns_400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/llm/openai/chat/completions", new { x = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("gemini", "models/gemini-2.0-flash:generateContent")]
    [InlineData("groq", "chat/completions")]
    [InlineData("mistral", "chat/completions")]
    public async Task Known_provider_without_key_returns_503(string provider, string path)
    {
        // appsettings.json do repo tem as keys vazias (placeholders) — fail-safe.
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/llm/{provider}/{path}", new { messages = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
