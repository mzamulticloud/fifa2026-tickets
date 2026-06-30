using Fifa2026.V2.FlowEvents.Models;

namespace Fifa2026.V2.FlowEvents.Data;

/// <summary>
/// Abstração da fonte de eventos do fluxo. A implementação real consulta o App
/// Insights (Log Analytics) por correlationId; os testes injetam um fake.
/// </summary>
public interface IFlowEventRepository
{
    /// <summary>
    /// AC-3/AC-4 — retorna a timeline de eventos (ordenada por timestamp asc) para um
    /// correlationId, derivada dos traces do App Insights de TODOS os 6 componentes.
    /// </summary>
    Task<IReadOnlyList<FlowEvent>> GetTimelineAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-5 — últimas N compras v2 (correlationId, timestamp, status) para a lista do front.
    /// </summary>
    Task<IReadOnlyList<RecentPurchase>> GetRecentPurchasesAsync(int top, CancellationToken cancellationToken = default);
}
