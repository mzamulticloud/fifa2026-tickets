using Fifa2026.V2.FlowEvents.Models;
using Microsoft.AspNetCore.SignalR;

namespace Fifa2026.V2.FlowEvents.Hubs;

/// <summary>
/// Implementação real do <see cref="IFlowEventPublisher"/> via IHubContext do FlowHub.
/// Envia o evento ("FlowEvent") só ao grupo correlation-&lt;id&gt; (AC-6).
/// </summary>
public sealed class SignalRFlowEventPublisher : IFlowEventPublisher
{
    /// <summary>Nome do método server→client (o front faz connection.on("FlowEvent", ...)).</summary>
    public const string ClientMethod = "FlowEvent";

    private readonly IHubContext<FlowHub> _hubContext;

    public SignalRFlowEventPublisher(IHubContext<FlowHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(FlowEvent flowEvent, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(FlowHub.GroupName(flowEvent.CorrelationId))
            .SendAsync(ClientMethod, flowEvent, cancellationToken);
}
