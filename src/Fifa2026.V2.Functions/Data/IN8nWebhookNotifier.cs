namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Story 2.4 AC-6/AC-7 — abstração do disparo fire-and-forget ao webhook do n8n.
/// Mantida como interface para permitir mock nos testes unitários do consumer
/// (verificar que é chamado em Inserted, NÃO em Duplicate, e que uma falha do n8n
/// não propaga — a mensagem do Service Bus jamais deve ir ao DLQ por causa do n8n).
/// </summary>
public interface IN8nWebhookNotifier
{
    /// <summary>
    /// Dispara o POST ao webhook do n8n com o payload pós-compra.
    /// Contrato fire-and-forget: NUNCA lança. Falha de rede/timeout/HTTP non-2xx é
    /// capturada e logada internamente — o chamador (consumer) não deve depender do
    /// retorno para decidir o fate da mensagem do Service Bus.
    /// </summary>
    Task NotifyPurchaseAsync(N8nWebhookPayload payload, CancellationToken cancellationToken = default);
}
