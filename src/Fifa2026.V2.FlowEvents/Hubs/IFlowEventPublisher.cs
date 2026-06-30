using Fifa2026.V2.FlowEvents.Models;

namespace Fifa2026.V2.FlowEvents.Hubs;

/// <summary>
/// AC-3/AC-6 — abstração do push de eventos via SignalR para o grupo correlation-&lt;id&gt;.
/// Testável (o endpoint usa esta interface; os testes verificam o broadcast sem um
/// SignalR Service real).
/// </summary>
public interface IFlowEventPublisher
{
    /// <summary>Empurra um evento do fluxo para todos os clientes assinantes do correlationId.</summary>
    Task PublishAsync(FlowEvent flowEvent, CancellationToken cancellationToken = default);
}
