using System.Text.Json.Serialization;

namespace Fifa2026.V2.FlowEvents.Models;

/// <summary>
/// Um evento tipado do fluxo de compra v2, derivado de um trace do App Insights
/// (customDimensions.CorrelationId == correlationId). É o payload empurrado via
/// SignalR (FlowHub.SendFlowEvent) e também retornado pela timeline REST
/// (GET /api/flow/{correlationId}).
///
/// Mapeia 1:1 com um nó do diagrama frontend (FlowDiagram) via <see cref="EventType"/>.
/// </summary>
public sealed class FlowEvent
{
    /// <summary>Correlação ponta-a-ponta (Gateway YARP nó 0 → SQL nó 5). ADE-000 Inv 5.</summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Tipo do evento = nó do diagrama (GATEWAY_YARP_RECEIVED .. SQL_INSERTED).</summary>
    [JsonPropertyName("eventType")]
    public FlowEventType EventType { get; set; }

    /// <summary>Índice ordinal do nó (0..5) — usado pela animação da bolinha.</summary>
    [JsonPropertyName("nodeIndex")]
    public int NodeIndex => (int)EventType;

    /// <summary>Timestamp do trace (UTC) — quando o hop ocorreu.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Duração gasta no hop em milissegundos (null se o trace não reportou).</summary>
    [JsonPropertyName("durationMs")]
    public double? DurationMs { get; set; }

    /// <summary>Status do hop: "ok" | "error".</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    /// <summary>Mensagem/payload inspecionável do trace (shadcn Sheet no front).</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
