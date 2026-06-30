using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Models;

/// <summary>
/// Resposta de GET /api/v2/purchase/{correlationId}.
/// status: queued | processing | completed | failed (blueprint 4.F1.③).
/// ticketId é o id da compra (purchases.id) quando concluída.
/// </summary>
public sealed class PurchaseStatusResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "queued";

    [JsonPropertyName("ticketId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TicketId { get; set; }
}
