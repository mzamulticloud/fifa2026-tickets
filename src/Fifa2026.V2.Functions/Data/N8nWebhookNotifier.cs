using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Story 2.4 AC-6/AC-7 — implementação fire-and-forget do disparo ao webhook do n8n.
///
/// Regras (NON-NEGOTIABLE):
///  - URL via App Setting <c>N8N_WEBHOOK_URL</c> (NUNCA hardcoded). Se ausente/vazia,
///    o disparo é um no-op silencioso (o fluxo F4 é opcional para alunos que ainda não
///    subiram o n8n — não pode quebrar o consumer F1).
///  - Timeout de 5s via CancellationTokenSource encadeado ao token do host.
///  - QUALQUER falha (timeout, rede, HTTP non-2xx) é capturada e logada — NUNCA
///    re-lançada. Falha do n8n não pode mandar a mensagem do Service Bus ao DLQ.
///  - Não loga token nem entraOid em texto (só correlationId, que já é o id de hop).
/// </summary>
public sealed class N8nWebhookNotifier : IN8nWebhookNotifier
{
    /// <summary>App Setting que contém a URL do webhook do n8n (ex.: https://&lt;fqdn&gt;/webhook/purchase).</summary>
    public const string WebhookUrlSetting = "N8N_WEBHOOK_URL";

    /// <summary>Timeout do disparo — o consumer não pode bloquear o processamento do Service Bus.</summary>
    private static readonly TimeSpan WebhookTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly string? _webhookUrl;
    private readonly ILogger<N8nWebhookNotifier> _logger;

    public N8nWebhookNotifier(HttpClient httpClient, IConfiguration configuration, ILogger<N8nWebhookNotifier> logger)
    {
        _httpClient = httpClient;
        _webhookUrl = configuration[WebhookUrlSetting];
        _logger = logger;
    }

    public async Task NotifyPurchaseAsync(N8nWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            // F4 não configurado neste ambiente — no-op. Não é erro: o aluno pode estar
            // numa fase anterior à F4 ou ainda não ter subido o n8n.
            _logger.LogDebug(
                "{Setting} não configurado — webhook n8n ignorado (correlationId={CorrelationId}).",
                WebhookUrlSetting, payload.CorrelationId);
            return;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(WebhookTimeout);

            using var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload, timeoutCts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Webhook n8n disparado com sucesso (correlationId={CorrelationId}, status={Status}).",
                    payload.CorrelationId, (int)response.StatusCode);
            }
            else
            {
                // HTTP non-2xx do n8n NÃO é falha do consumer — logamos e seguimos.
                _logger.LogWarning(
                    "Webhook n8n respondeu non-2xx (correlationId={CorrelationId}, status={Status}). " +
                    "A compra já foi gravada; a mensagem do Service Bus NÃO vai ao DLQ.",
                    payload.CorrelationId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget: timeout (5s), rede indisponível, DNS, etc. — tudo capturado.
            // A compra já está no SQL; o webhook é best-effort. NUNCA re-throw.
            _logger.LogWarning(
                ex,
                "Falha ao disparar webhook n8n (correlationId={CorrelationId}). A compra já foi gravada; " +
                "a mensagem do Service Bus NÃO vai ao DLQ por falha do n8n.",
                payload.CorrelationId);
        }
    }
}
