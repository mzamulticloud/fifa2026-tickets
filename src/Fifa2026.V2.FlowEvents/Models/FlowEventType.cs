namespace Fifa2026.V2.FlowEvents.Models;

/// <summary>
/// Os 6 tipos de evento do Flow Visualizer (Story 2.6 Dev Notes "Event types").
/// A ORDEM dos membros É a ordem dos hops no diagrama — o NÓ ZERO é o Gateway YARP
/// (ADE-004), NUNCA APIM (APIM não existe no EPIC-002).
///
/// Cada membro corresponde a 1 nó do diagrama frontend (FlowDiagram). O índice
/// ordinal (0..5) é o número do nó usado pela animação da "bolinha".
/// </summary>
public enum FlowEventType
{
    /// <summary>Nó 0 — Gateway YARP recebe a request e injeta X-Correlation-ID (nó zero do tracing).</summary>
    GATEWAY_YARP_RECEIVED = 0,

    /// <summary>Nó 1 — PurchaseEntryFunction processa e publica no Service Bus.</summary>
    FUNCTION_ENTRY_PROCESSED = 1,

    /// <summary>Nó 2 — mensagem publicada na fila tickets-purchase do Service Bus.</summary>
    SERVICE_BUS_PUBLISHED = 2,

    /// <summary>Nó 3 — PurchaseConsumerFunction consome, grava no SQL e dispara o n8n.</summary>
    FUNCTION_CONSUMER_DONE = 3,

    /// <summary>Nó 4 — webhook do n8n disparado (workflow post-purchase-notification).</summary>
    N8N_WEBHOOK_TRIGGERED = 4,

    /// <summary>Nó 5 — linha gravada em purchases.correlation_id no SQL.</summary>
    SQL_INSERTED = 5
}
