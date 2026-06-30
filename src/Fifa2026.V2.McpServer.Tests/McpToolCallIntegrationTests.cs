using System.Text.Json;
using Fifa2026.V2.McpServer.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using Moq;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// AC-2/3 ponta-a-ponta: prova que tools/call REALMENTE despacha para o handler com
/// DI funcionando — o SDK injeta IFifaQueryRepository (mockado aqui) e EntraOidContext
/// nos parâmetros do método da tool. Substitui o repositório por um mock via
/// WebApplicationFactory.WithWebHostBuilder (ConfigureTestServices), então NÃO toca SQL.
/// </summary>
public sealed class McpToolCallIntegrationTests
{
    [Fact]
    public async Task ToolsCall_consultar_disponibilidade_dispatches_to_handler_with_DI()
    {
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarDisponibilidadeAsync(7, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AvailabilityResult
            {
                Encontrado = true,
                Partida = "Brasil x Argentina",
                VipDisponivel = 3,
                PrecoVip = 999m,
            });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                // Substitui o repositório real (Dapper/SQL) pelo mock.
                services.AddSingleton(repo.Object);
            });
        });

        var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null!,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport);

        var result = await mcpClient.CallToolAsync(
            "consultar_disponibilidade",
            new Dictionary<string, object?> { ["matchId"] = 7 }!);

        // O resultado da tool deve refletir o mock (DI funcionou). O SDK pode
        // entregar o resultado em StructuredContent (objeto) e/ou no Content textual.
        var structured = result.StructuredContent.HasValue
            ? JsonSerializer.Serialize(result.StructuredContent.Value)
            : string.Empty;
        var textual = string.Join(
            "\n",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        var combined = structured + "\n" + textual;

        Assert.False(result.IsError ?? false, $"tool retornou erro. Content={textual}");
        Assert.Contains("Brasil x Argentina", combined);
        repo.Verify(r => r.ConsultarDisponibilidadeAsync(7, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Story 2.8 AC-7 — prova que uma das novas tools da Fase A (consultar_partidas)
    /// despacha end-to-end via tools/call com DI funcionando (SDK injeta o repositório
    /// mockado). Não toca SQL real.
    /// </summary>
    [Fact]
    public async Task ToolsCall_consultar_partidas_dispatches_to_handler_with_DI()
    {
        var repo = new Mock<IFifaQueryRepository>();
        repo.Setup(r => r.ConsultarPartidasAsync(
                "Brasil", null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchResult>
            {
                new() { Partida = "Brasil x Sérvia", Fase = "Fase de Grupos", Grupo = "A", Status = "scheduled" }
            });

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services => services.AddSingleton(repo.Object));
        });

        var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null!,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport);

        var result = await mcpClient.CallToolAsync(
            "consultar_partidas",
            new Dictionary<string, object?> { ["time"] = "Brasil" }!);

        var structured = result.StructuredContent.HasValue
            ? JsonSerializer.Serialize(result.StructuredContent.Value)
            : string.Empty;
        var textual = string.Join(
            "\n",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        var combined = structured + "\n" + textual;

        Assert.False(result.IsError ?? false, $"tool retornou erro. Content={textual}");
        // Substring ASCII-safe: o StructuredContent escapa acentos como é (Sérvia).
        Assert.Contains("Brasil x S", combined);
        repo.Verify(r => r.ConsultarPartidasAsync(
            "Brasil", null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Story 2.8 AC-5 / Story 2.9 AC-6/AC-10 — após a Fase B, tools/list expõe exatamente
    /// 8 tools: 7 read-only ("sentidos", readOnly=true) + 1 de ação ("mão",
    /// criar_alerta_ingresso, readOnly=false/ausente). O discriminador ReadOnly é a
    /// propriedade estrutural auditável da ADE-006 Inv 2.
    /// </summary>
    [Fact]
    public async Task ToolsList_returns_eight_tools_with_readonly_discriminator()
    {
        var repo = new Mock<IFifaQueryRepository>();

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services => services.AddSingleton(repo.Object));
        });

        var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null!,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport);

        var tools = await mcpClient.ListToolsAsync();

        Assert.Equal(8, tools.Count);

        var names = tools.Select(t => t.Name).ToHashSet();
        foreach (var expected in new[]
                 {
                     "consultar_disponibilidade", "verificar_ingresso", "consultar_bracket",
                     "consultar_partidas", "consultar_classificacao", "consultar_time", "consultar_estadio",
                     "criar_alerta_ingresso"
                 })
        {
            Assert.Contains(expected, names);
        }

        // AC-10 — discriminador ReadOnly: exatamente 7 sentidos (readOnlyHint=true) e
        // 1 mão (readOnlyHint false/ausente), e a única mão é criar_alerta_ingresso.
        var readOnlyTools = tools.Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint == true).ToList();
        var actionTools = tools.Where(t => t.ProtocolTool.Annotations?.ReadOnlyHint != true).ToList();
        Assert.Equal(7, readOnlyTools.Count);
        var actionTool = Assert.Single(actionTools);
        Assert.Equal("criar_alerta_ingresso", actionTool.Name);
    }

    /// <summary>
    /// Story 2.9 AC-9 — prova que tools/call criar_alerta_ingresso despacha end-to-end
    /// com DI funcionando (SDK injeta IAlertWebhookNotifier mockado — nenhum n8n real)
    /// e que o retorno AlertResult serializa com os nomes PT-BR do contrato.
    /// </summary>
    [Fact]
    public async Task ToolsCall_criar_alerta_ingresso_dispatches_to_handler_with_DI()
    {
        var repo = new Mock<IFifaQueryRepository>();
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.AddSingleton(repo.Object);
                // Substitui o AlertWebhookNotifier real (HTTP → n8n) pelo mock.
                services.AddSingleton(notifier.Object);
            });
        });

        var httpClient = factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            loggerFactory: null!,
            ownsHttpClient: false);

        await using var mcpClient = await McpClient.CreateAsync(transport);

        var result = await mcpClient.CallToolAsync(
            "criar_alerta_ingresso",
            new Dictionary<string, object?>
            {
                ["matchDescription"] = "final",
                ["categoria"] = "VIP",
            }!);

        var structured = result.StructuredContent.HasValue
            ? JsonSerializer.Serialize(result.StructuredContent.Value)
            : string.Empty;
        var textual = string.Join(
            "\n",
            result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(c => c.Text));
        var combined = structured + "\n" + textual;

        Assert.False(result.IsError ?? false, $"tool retornou erro. Content={textual}");
        // Contrato AC-5: campos PT-BR "registrado"/"mensagem" no retorno serializado.
        Assert.Contains("registrado", combined);
        notifier.Verify(n => n.NotifyAlertAsync(
            It.Is<AlertWebhookPayload>(p =>
                p.MatchDescription == "final" &&
                p.Categoria == "VIP Premium" &&
                p.CorrelationId != Guid.Empty),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
