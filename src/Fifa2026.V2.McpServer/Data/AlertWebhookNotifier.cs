using System.Net.Http.Json;

namespace Fifa2026.V2.McpServer.Data;

/// <summary>
/// Story 2.9 AC-4 — implementação fire-and-forget do disparo de alerta ao n8n.
/// Mirror estrutural de <c>N8nWebhookNotifier</c> de F4 (assemblies independentes).
///
/// REGRA DE OURO (ADE-006 Inv 1): esta classe só faz HTTP POST — zero SQL. A "mão"
/// age exclusivamente acionando a orquestração no n8n; nenhuma escrita direta no
/// banco acontece no McpServer.
///
/// Graceful degradation: sem <c>N8N_ALERT_WEBHOOK_URL</c> configurada, o disparo é
/// no-op silencioso (LogDebug + false) — padrão F4. Qualquer falha retorna false;
/// NUNCA re-throw (o chat não pode quebrar porque o n8n está fora do ar).
/// </summary>
public sealed class AlertWebhookNotifier : IAlertWebhookNotifier
{
    /// <summary>App Setting com a URL do webhook do workflow <c>chat-alert-ingresso</c>.
    /// DISTINTO de <c>N8N_WEBHOOK_URL</c> (F4, workflow post-purchase-notification) —
    /// misturar os dois faria a compra acionar o alerta e vice-versa (Gotcha 1).</summary>
    public const string WebhookUrlSetting = "N8N_ALERT_WEBHOOK_URL";

    /// <summary>Nome do named client registrado em Program.cs (timeout 5s).</summary>
    public const string HttpClientName = "n8n-alert";

    private static readonly TimeSpan WebhookTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _webhookUrl;
    private readonly ILogger<AlertWebhookNotifier> _logger;

    public AlertWebhookNotifier(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlertWebhookNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _webhookUrl = configuration[WebhookUrlSetting];
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookUrl);

    public async Task<bool> NotifyAlertAsync(AlertWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogDebug("{Setting} não configurado — disparo de alerta ignorado.", WebhookUrlSetting);
            return false;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(WebhookTimeout);

            using var response = await client.PostAsJsonAsync(_webhookUrl, payload, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Webhook de alerta disparado correlationId={CorrelationId} status={StatusCode}",
                    payload.CorrelationId, (int)response.StatusCode);
                return true;
            }

            _logger.LogWarning(
                "Webhook de alerta retornou non-2xx correlationId={CorrelationId} status={StatusCode}",
                payload.CorrelationId, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            // Fire-and-forget: timeout/rede/serialização — loga e segue (nunca re-throw).
            _logger.LogWarning(ex,
                "Falha ao disparar webhook de alerta correlationId={CorrelationId}",
                payload.CorrelationId);
            return false;
        }
    }
}
