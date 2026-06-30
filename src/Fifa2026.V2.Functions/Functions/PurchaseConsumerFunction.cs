using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// AC-4/AC-6/AC-7 — Consumidor do fluxo de compra v2.
/// Service Bus trigger em `tickets-purchase` → INSERT idempotente em `purchases`
/// (source='v2', status='completed') via UNIQUE constraint + catch SqlException 2627.
///
/// Tratamento de falhas (AC-7 / DLQ):
///  - Duplicata (2627) → ignorada silenciosamente (idempotência atingida).
///  - matchId/category inválidos → falha PERMANENTE: re-throw para que, após
///    maxDeliveryCount (10), a mensagem caia automaticamente no DLQ.
///  - Erros transitórios (timeout SQL etc.) → re-throw → reentrega → eventual DLQ.
/// </summary>
public sealed class PurchaseConsumerFunction
{
    private readonly IPurchaseRepository _repository;
    private readonly IN8nWebhookNotifier _n8nNotifier;
    private readonly ILogger<PurchaseConsumerFunction> _logger;

    public PurchaseConsumerFunction(
        IPurchaseRepository repository,
        IN8nWebhookNotifier n8nNotifier,
        ILogger<PurchaseConsumerFunction> logger)
    {
        _repository = repository;
        _n8nNotifier = n8nNotifier;
        _logger = logger;
    }

    [Function(nameof(PurchaseConsumerFunction))]
    public async Task RunAsync(
        [ServiceBusTrigger("tickets-purchase", Connection = "ServiceBusConnection")] string messageBody,
        CancellationToken cancellationToken)
    {
        PurchaseMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<PurchaseMessage>(
                messageBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            // Mensagem malformada não tem como ser reprocessada com sucesso → DLQ.
            _logger.LogError(ex, "Mensagem com JSON inválido em tickets-purchase. Será encaminhada ao DLQ.");
            throw;
        }

        if (message is null || message.CorrelationId == Guid.Empty)
        {
            _logger.LogError("Mensagem sem correlationId válido. Será encaminhada ao DLQ.");
            throw new InvalidOperationException("Mensagem inválida: correlationId ausente.");
        }

        // BeginScope propaga o correlationId para o App Insights (ADE-000 Inv 5 — log hop).
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId }))
        {
            _logger.LogInformation(
                "Processando compra v2: matchId={MatchId} category={Category} userId={UserId} quantity={Quantity}",
                message.MatchId, message.Category, message.UserId, message.Quantity);

            var outcome = await _repository.InsertPurchaseAsync(message, cancellationToken);

            switch (outcome)
            {
                case InsertOutcome.Inserted:
                    _logger.LogInformation("Compra v2 gravada com sucesso (correlationId={CorrelationId}).", message.CorrelationId);

                    // Story 2.4 AC-6/AC-7 — APENAS em Inserted (não em Duplicate): dispara o
                    // webhook do n8n para a orquestração pós-compra (e-mail mock, log, etc.).
                    // Fire-and-forget: o notifier já encapsula timeout (5s) e try/catch e
                    // NUNCA lança. O try/catch abaixo é defesa em profundidade — mesmo que o
                    // notifier viole o contrato, a compra JÁ foi gravada e a mensagem do
                    // Service Bus NUNCA pode ir ao DLQ por falha do n8n. O payload sai do
                    // CORPO da mensagem (correlationId/entraOid), não das Application
                    // Properties do Service Bus.
                    try
                    {
                        await _n8nNotifier.NotifyPurchaseAsync(
                            new N8nWebhookPayload
                            {
                                CorrelationId = message.CorrelationId,
                                MatchId = message.MatchId,
                                Category = message.Category,
                                EntraOid = message.EntraOid
                            },
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Falha inesperada no disparo do webhook n8n (correlationId={CorrelationId}). " +
                            "A compra já foi gravada; a mensagem do Service Bus NÃO vai ao DLQ.",
                            message.CorrelationId);
                    }
                    break;

                case InsertOutcome.Duplicate:
                    // Idempotência: mensagem reentregue. Completa sem erro (não vai para DLQ).
                    _logger.LogInformation("Compra v2 já existente — duplicata ignorada (correlationId={CorrelationId}).", message.CorrelationId);
                    break;

                case InsertOutcome.CategoryNotFound:
                    // Falha permanente: re-throw força reentrega → DLQ após maxDeliveryCount (AC-7).
                    _logger.LogError(
                        "Categoria inexistente para matchId={MatchId} category={Category}. Encaminhando ao DLQ.",
                        message.MatchId, message.Category);
                    throw new InvalidOperationException(
                        $"Nenhuma ticket_category para matchId={message.MatchId}, category={message.Category}.");
            }
        }
    }
}
