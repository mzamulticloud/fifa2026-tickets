using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Story 2.4 AC-6/AC-7 — corpo JSON enviado ao webhook do n8n após uma compra v2 ser
/// efetivamente gravada (InsertOutcome.Inserted). Os campos saem do CORPO da
/// PurchaseMessage (não das Application Properties do Service Bus) — correlationId e
/// entraOid já viajam serializados no body da mensagem (ver Models/PurchaseMessage.cs).
///
/// Observação (Art. IV — No Invention): o blueprint da AC-6 lista um campo `amount`,
/// porém a PurchaseMessage de F1 NÃO carrega valor monetário no corpo — o unit_price
/// só é resolvido no INSERT do SQL (JOIN em ticket_categories). Para não inventar um
/// valor, o payload inclui apenas os campos realmente presentes na mensagem:
/// correlationId, matchId, category e entraOid. O n8n usa correlationId para o log e
/// matchId/category para a ramificação (Switch VIP vs. outros).
/// </summary>
public sealed class N8nWebhookPayload
{
    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; init; }

    [JsonPropertyName("matchId")]
    public int MatchId { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Object ID (claim `oid`) do usuário no tenant Entra, propagado pelo gateway via
    /// X-Entra-OID (ADE-005). NULL para fluxos sem identidade Entra. Nunca é logado em
    /// texto pelo consumer (AC — não logar token/oid).
    /// </summary>
    [JsonPropertyName("entraOid")]
    public Guid? EntraOid { get; init; }
}
