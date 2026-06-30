using System.Text.Json.Serialization;

namespace Fifa2026.V2.FlowEvents.Models;

/// <summary>
/// AC-5 — item da lista "últimas 50 compras" no front (sortable/searchable por
/// correlationId). Derivada dos traces de entrada (GATEWAY_YARP_RECEIVED) no App Insights.
/// </summary>
public sealed class RecentPurchase
{
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>"ok" | "error" — pior status observado entre os traces dessa compra.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";
}
