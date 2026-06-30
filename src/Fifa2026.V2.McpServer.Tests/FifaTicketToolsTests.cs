using Fifa2026.V2.McpServer.Data;
using Fifa2026.V2.McpServer.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Testes das 3 tools MCP (AC-3/4/5). As tools são métodos estáticos que recebem
/// o data layer por DI — mockamos <see cref="IFifaQueryRepository"/> (mesmo padrão
/// de mock do data layer usado em N8nWebhookNotifierTests/PurchaseStatusFunctionTests).
///
/// Cada teste confirma que a tool: (a) repassa os parâmetros corretos ao repositório
/// e (b) devolve o resultado do repositório. O EntraOidContext é construído sobre um
/// IHttpContextAccessor com header X-Entra-OID controlado (AC-9 — lido só p/ logging).
/// </summary>
public sealed class FifaTicketToolsTests
{
    private static readonly NullLogger<FifaTicketTools.DiagnosticsCategory> Log =
        NullLogger<FifaTicketTools.DiagnosticsCategory>.Instance;

    private static EntraOidContext OidContext(string? oidHeader)
    {
        var ctx = new DefaultHttpContext();
        if (oidHeader is not null)
        {
            ctx.Request.Headers[EntraOidContext.HeaderName] = oidHeader;
        }
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(ctx);
        return new EntraOidContext(accessor.Object);
    }

    [Fact]
    public async Task ConsultarDisponibilidade_passes_args_and_returns_repository_result()
    {
        var expected = new AvailabilityResult
        {
            Encontrado = true,
            Partida = "Brasil x Argentina",
            VipDisponivel = 5,
            PrecoVip = 1200m
        };
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarDisponibilidadeAsync(42, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await FifaTicketTools.ConsultarDisponibilidadeAsync(
            repo.Object, OidContext("11111111-2222-3333-4444-555555555555"), Log,
            matchId: 42, matchDescription: null, CancellationToken.None);

        Assert.Same(expected, result);
        repo.Verify(r => r.ConsultarDisponibilidadeAsync(42, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultarDisponibilidade_by_description_passes_string()
    {
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarDisponibilidadeAsync(null, "Brasil x Argentina", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvailabilityResult { Encontrado = false });

        var result = await FifaTicketTools.ConsultarDisponibilidadeAsync(
            repo.Object, OidContext(oidHeader: null), Log,
            matchId: null, matchDescription: "Brasil x Argentina", CancellationToken.None);

        Assert.False(result.Encontrado);
        repo.Verify(r => r.ConsultarDisponibilidadeAsync(null, "Brasil x Argentina", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerificarIngresso_passes_id_and_returns_result()
    {
        var expected = new TicketVerificationResult { Valido = true, Comprador = "Alice", Categoria = "VIP" };
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.VerificarIngressoAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await FifaTicketTools.VerificarIngressoAsync(
            repo.Object, OidContext("aaaa"), Log, ingressoId: 123, CancellationToken.None);

        Assert.True(result.Valido);
        Assert.Equal("Alice", result.Comprador);
        repo.Verify(r => r.VerificarIngressoAsync(123, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultarBracket_passes_rodada_and_returns_list()
    {
        var expected = new List<BracketMatchResult>
        {
            new() { Jogo = "Brasil x França", Status = "scheduled" }
        };
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarBracketAsync("oitavas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await FifaTicketTools.ConsultarBracketAsync(
            repo.Object, OidContext(oidHeader: null), Log, rodada: "oitavas", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Brasil x França", result[0].Jogo);
        repo.Verify(r => r.ConsultarBracketAsync("oitavas", It.IsAny<CancellationToken>()), Times.Once);
    }
}
