using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Functions;
using Fifa2026.V2.Functions.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// AC-4/AC-6/AC-7 (F1) — comportamento do consumer: happy path, idempotência (duplicata
/// não falha), e falha permanente (categoria inexistente → re-throw → DLQ).
/// Story 2.4 (F4) — disparo do webhook n8n: APENAS em Inserted, NUNCA em Duplicate, e
/// falha do n8n NÃO propaga (não vai para DLQ).
/// </summary>
public sealed class PurchaseConsumerFunctionTests
{
    private static string Serialize(PurchaseMessage message) => JsonSerializer.Serialize(message);

    private static PurchaseMessage NewMessage() => new()
    {
        CorrelationId = Guid.NewGuid(),
        MatchId = 1,
        Category = "VIP",
        UserId = 7,
        Quantity = 2
    };

    /// <summary>Notifier mock que não faz nada (default) — usado pelos testes que não focam no webhook.</summary>
    private static Mock<IN8nWebhookNotifier> NoopNotifier()
    {
        var notifier = new Mock<IN8nWebhookNotifier>();
        notifier
            .Setup(n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return notifier;
    }

    private static PurchaseConsumerFunction Build(IPurchaseRepository repo, IN8nWebhookNotifier notifier) =>
        new(repo, notifier, NullLogger<PurchaseConsumerFunction>.Instance);

    [Fact]
    public async Task Happy_path_inserts_and_completes()
    {
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var sut = Build(repo.Object, NoopNotifier().Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Passes_EntraOid_Through_To_Repository()
    {
        // Story 2.3 AC-9 — o consumer repassa o entra_oid (claim oid do gateway) para
        // o repositório, que o grava na coluna entra_oid (verificado via captura do arg).
        var oid = Guid.Parse("55555555-6666-7777-8888-999999999999");
        PurchaseMessage? captured = null;

        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(InsertOutcome.Inserted);

        var sut = Build(repo.Object, NoopNotifier().Object);

        var message = NewMessage();
        message.EntraOid = oid;
        await sut.RunAsync(Serialize(message), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(oid, captured!.EntraOid);
    }

    [Fact]
    public async Task Duplicate_is_swallowed_silently_no_throw()
    {
        // AC-6: enviar a mesma mensagem 2x → consumer NÃO lança (não vai para DLQ).
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Duplicate);

        var sut = Build(repo.Object, NoopNotifier().Object);

        var exception = await Record.ExceptionAsync(() => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CategoryNotFound_throws_to_route_to_dlq()
    {
        // AC-7: matchId/category inválidos → falha permanente → re-throw → DLQ.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.CategoryNotFound);

        var sut = Build(repo.Object, NoopNotifier().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_json_throws_to_route_to_dlq()
    {
        var repo = new Mock<IPurchaseRepository>();
        var sut = Build(repo.Object, NoopNotifier().Object);

        await Assert.ThrowsAsync<JsonException>(
            () => sut.RunAsync("{ not valid json", CancellationToken.None));

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_correlationId_throws()
    {
        var repo = new Mock<IPurchaseRepository>();
        var sut = Build(repo.Object, NoopNotifier().Object);

        var message = NewMessage();
        message.CorrelationId = Guid.Empty;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(message), CancellationToken.None));

        repo.Verify(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------------------------------------------------------------------------
    // Story 2.4 (F4) — webhook n8n
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Webhook_n8n_is_fired_on_Inserted()
    {
        // Story 2.4 AC-6: após InsertOutcome.Inserted, o consumer dispara o webhook n8n.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var notifier = NoopNotifier();
        var sut = Build(repo.Object, notifier.Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Webhook_n8n_payload_carries_correlationId_and_entraOid_from_body()
    {
        // Story 2.4 AC-6/AC-7: o payload do webhook sai do CORPO da mensagem e inclui
        // correlationId + entraOid (não das Application Properties do Service Bus).
        var oid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        N8nWebhookPayload? captured = null;

        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var notifier = new Mock<IN8nWebhookNotifier>();
        notifier
            .Setup(n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()))
            .Callback<N8nWebhookPayload, CancellationToken>((p, _) => captured = p)
            .Returns(Task.CompletedTask);

        var sut = Build(repo.Object, notifier.Object);

        var message = NewMessage();
        message.EntraOid = oid;
        await sut.RunAsync(Serialize(message), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(message.CorrelationId, captured!.CorrelationId);
        Assert.Equal(message.MatchId, captured.MatchId);
        Assert.Equal(message.Category, captured.Category);
        Assert.Equal(oid, captured.EntraOid);
    }

    [Fact]
    public async Task Webhook_n8n_is_NOT_fired_on_Duplicate()
    {
        // Story 2.4 Task 6.5: idempotência preservada — em Duplicate o n8n NÃO é chamado.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Duplicate);

        var notifier = NoopNotifier();
        var sut = Build(repo.Object, notifier.Object);

        await sut.RunAsync(Serialize(NewMessage()), CancellationToken.None);

        notifier.Verify(
            n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_n8n_is_NOT_fired_on_CategoryNotFound()
    {
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.CategoryNotFound);

        var notifier = NoopNotifier();
        var sut = Build(repo.Object, notifier.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));

        notifier.Verify(
            n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Webhook_n8n_failure_does_NOT_propagate_to_dlq()
    {
        // Story 2.4 AC-6/Task 6.4: o contrato do notifier é NUNCA lançar. Mas se um
        // notifier hipotético lançasse, a mensagem do Service Bus seria mandada ao DLQ
        // indevidamente. Este teste garante o contrato no nível do consumer: mesmo com
        // um notifier que lança, o consumer NÃO deve propagar (a compra já foi gravada).
        //
        // NOTA: a implementação real (N8nWebhookNotifier) já encapsula o try/catch e
        // nunca lança — coberto em N8nWebhookNotifierTests. Aqui validamos a fronteira
        // do consumer com um notifier que viola o contrato, garantindo defesa em
        // profundidade contra DLQ acidental.
        var repo = new Mock<IPurchaseRepository>();
        repo.Setup(r => r.InsertPurchaseAsync(It.IsAny<PurchaseMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(InsertOutcome.Inserted);

        var notifier = new Mock<IN8nWebhookNotifier>();
        notifier
            .Setup(n => n.NotifyPurchaseAsync(It.IsAny<N8nWebhookPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("n8n indisponível"));

        var sut = Build(repo.Object, notifier.Object);

        var exception = await Record.ExceptionAsync(
            () => sut.RunAsync(Serialize(NewMessage()), CancellationToken.None));

        Assert.Null(exception);
    }
}
