using Fifa2026.V2.McpServer.Data;
using Fifa2026.V2.McpServer.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// Story 2.9 AC-9 — testes da primeira tool de AÇÃO (criar_alerta_ingresso, ReadOnly=false).
/// Mesmo padrão de <see cref="FifaAgenticToolsTests"/>: método estático com dependências por
/// DI — mockamos <see cref="IAlertWebhookNotifier"/> (nenhum n8n real). Cobrem: sucesso,
/// falha fire-and-forget (sem throw), validação de identificadores ausentes, mapeamento de
/// categoria (rótulo real ou null — anti-alucinação) e conteúdo do payload (correlationId
/// novo + entraOid raw como dado, ADE-006 Inv 7).
/// </summary>
public sealed class FifaActionToolsTests
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
    public async Task CriarAlerta_success_returns_registrado_true_and_builds_payload()
    {
        const string oid = "11111111-2222-3333-4444-555555555555";
        AlertWebhookPayload? captured = null;
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AlertWebhookPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(true);

        var result = await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oid), Log,
            matchId: 42, matchDescription: "final", categoria: "VIP",
            cancellationToken: CancellationToken.None);

        Assert.True(result.Registrado);
        Assert.Equal("Alerta registrado. Você será notificado quando houver disponibilidade.", result.Mensagem);
        notifier.Verify(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(captured);
        Assert.NotEqual(Guid.Empty, captured!.CorrelationId);
        Assert.Equal(oid, captured.EntraOid);
        Assert.Equal(42, captured.MatchId);
        Assert.Equal("final", captured.MatchDescription);
        Assert.Equal("VIP Premium", captured.Categoria);
        Assert.NotEqual(default, captured.RequestedAt);
    }

    [Fact]
    public async Task CriarAlerta_webhook_failure_returns_registrado_false_without_throw()
    {
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchDescription: "Brasil x Argentina",
            cancellationToken: CancellationToken.None);

        Assert.False(result.Registrado);
        Assert.Equal("Não foi possível registrar o alerta. Tente novamente mais tarde.", result.Mensagem);
        notifier.Verify(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CriarAlerta_without_matchId_and_matchDescription_returns_error_and_skips_notifier()
    {
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);

        var result = await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchId: null, matchDescription: null, categoria: "VIP",
            cancellationToken: CancellationToken.None);

        Assert.False(result.Registrado);
        Assert.Equal("Informe matchId ou matchDescription para criar o alerta.", result.Mensagem);
        notifier.Verify(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAlerta_maps_categoria_vip_to_real_db_label()
    {
        AlertWebhookPayload? captured = null;
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AlertWebhookPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(true);

        await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchDescription: "final", categoria: "VIP",
            cancellationToken: CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("VIP Premium", captured!.Categoria);
    }

    [Fact]
    public async Task CriarAlerta_unknown_or_null_categoria_sends_null_label()
    {
        var sentLabels = new List<string?>();
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AlertWebhookPayload, CancellationToken>((p, _) => sentLabels.Add(p.Categoria))
            .ReturnsAsync(true);

        await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchDescription: "final", categoria: "Camarote Diamante",
            cancellationToken: CancellationToken.None);

        await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchDescription: "final", categoria: null,
            cancellationToken: CancellationToken.None);

        // Rótulo desconhecido e categoria omitida → null (nenhum rótulo inventado).
        Assert.Equal(new string?[] { null, null }, sentLabels);
    }

    [Fact]
    public async Task CriarAlerta_unconfigured_webhook_returns_specific_message_and_skips_dispatch()
    {
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(false);

        var result = await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchDescription: "final",
            cancellationToken: CancellationToken.None);

        // AC-3 graceful degradation: mensagem ESPECÍFICA de ambiente sem webhook,
        // distinta da falha transitória; nenhum POST tentado.
        Assert.False(result.Registrado);
        Assert.Equal("Webhook de alerta não configurado neste ambiente.", result.Mensagem);
        notifier.Verify(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarAlerta_without_oid_header_sends_null_entraOid()
    {
        AlertWebhookPayload? captured = null;
        var notifier = new Mock<IAlertWebhookNotifier>();
        notifier.SetupGet(n => n.IsConfigured).Returns(true);
        notifier.Setup(n => n.NotifyAlertAsync(It.IsAny<AlertWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AlertWebhookPayload, CancellationToken>((p, _) => captured = p)
            .ReturnsAsync(true);

        await FifaActionTools.CriarAlertaIngressoAsync(
            notifier.Object, OidContext(oidHeader: null), Log,
            matchId: 7,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Null(captured!.EntraOid);
        Assert.Equal(7, captured.MatchId);
    }
}
