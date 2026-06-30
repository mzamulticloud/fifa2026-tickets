using Microsoft.AspNetCore.SignalR;

namespace Fifa2026.V2.FlowEvents.Hubs;

/// <summary>
/// AC-3/AC-6 — Hub SignalR (Service Mode: Default, Hub clássico) do Flow Visualizer.
/// O cliente assina o grupo `correlation-&lt;id&gt;` ao selecionar uma compra; o servidor
/// empurra eventos do fluxo (FlowEvent) só para esse grupo via método "FlowEvent".
///
/// Client subscribe:  connection.invoke("Subscribe", correlationId)
/// Server → client:   connection.on("FlowEvent", handler)  (Story 2.6 Dev Notes "SignalR setup")
/// </summary>
public sealed class FlowHub : Hub
{
    /// <summary>Prefixo do grupo SignalR por correlationId (1 grupo por compra observada).</summary>
    public static string GroupName(string correlationId) => $"correlation-{correlationId}";

    /// <summary>O cliente entra no grupo da compra que quer observar em tempo real.</summary>
    public Task Subscribe(string correlationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(correlationId));

    /// <summary>O cliente sai do grupo (ao trocar de compra ou fechar a visualização).</summary>
    public Task Unsubscribe(string correlationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(correlationId));
}
