using System.Text.Json.Serialization;

namespace Fifa2026.V2.McpServer.Data;

/// <summary>
/// Story 2.9 AC-3/AC-5 — corpo JSON enviado pela tool <c>criar_alerta_ingresso</c>
/// ao webhook n8n do workflow <c>chat-alert-ingresso</c> (App Setting
/// <c>N8N_ALERT_WEBHOOK_URL</c> — distinto de <c>N8N_WEBHOOK_URL</c> de F4).
///
/// <c>EntraOid</c> é <c>string?</c> (valor bruto do header <c>X-Entra-OID</c> via
/// <see cref="Tools.EntraOidContext.GetRawOid"/>) — o McpServer NÃO faz parse para
/// Guid (diferente do <c>N8nWebhookPayload</c> de F4). Identidade viaja como DADO
/// no payload, não como credencial (ADE-006 Inv 7) — e NUNCA aparece em log raw.
/// </summary>
public sealed class AlertWebhookPayload
{
    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; init; }

    [JsonPropertyName("entraOid")]
    public string? EntraOid { get; init; }

    [JsonPropertyName("matchId")]
    public int? MatchId { get; init; }

    [JsonPropertyName("matchDescription")]
    public string? MatchDescription { get; init; }

    [JsonPropertyName("categoria")]
    public string? Categoria { get; init; }

    [JsonPropertyName("requestedAt")]
    public DateTimeOffset RequestedAt { get; init; }
}

/// <summary>
/// Story 2.9 AC-2/AC-5 — retorno da tool <c>criar_alerta_ingresso</c> ao LLM/front.
/// Fire-and-forget: falha do webhook vira <c>Registrado = false</c> com mensagem
/// descritiva — nunca exceção propagada (ADE-006 B.1).
/// </summary>
public sealed class AlertResult
{
    [JsonPropertyName("registrado")]
    public bool Registrado { get; init; }

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; init; } = string.Empty;
}
