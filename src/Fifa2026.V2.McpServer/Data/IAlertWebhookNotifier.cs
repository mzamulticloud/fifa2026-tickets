namespace Fifa2026.V2.McpServer.Data;

/// <summary>
/// Story 2.9 AC-4 — despacho fire-and-forget do alerta de ingresso ao webhook n8n
/// (workflow <c>chat-alert-ingresso</c>). Abstração testável (mockada nos testes da
/// tool <c>criar_alerta_ingresso</c>), espelho do padrão <c>IN8nWebhookNotifier</c>
/// de F4 (<c>src/Fifa2026.V2.Functions/Data/</c> — assemblies independentes, cópia
/// estrutural intencional como o <see cref="CategoryLabelMapper"/>).
/// </summary>
public interface IAlertWebhookNotifier
{
    /// <summary>
    /// <c>true</c> quando <c>N8N_ALERT_WEBHOOK_URL</c> está configurada. Permite à tool
    /// distinguir "ambiente sem webhook" (mensagem específica do AC-3, graceful
    /// degradation) de "falha no disparo" (mensagem genérica de retry).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// POSTa o payload no webhook n8n. Retorna <c>true</c> em 2xx; <c>false</c> em
    /// qualquer falha (URL ausente, timeout, rede, non-2xx) — NUNCA lança exceção.
    /// </summary>
    Task<bool> NotifyAlertAsync(AlertWebhookPayload payload, CancellationToken cancellationToken = default);
}
