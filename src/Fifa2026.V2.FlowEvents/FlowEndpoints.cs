using Fifa2026.V2.FlowEvents.Data;
using Fifa2026.V2.FlowEvents.Hubs;

namespace Fifa2026.V2.FlowEvents;

/// <summary>
/// AC-3/AC-5/AC-6 — endpoints HTTP do FlowEvents service:
///   GET  /api/flow/recent           → últimas N compras (lista do front, AC-5)
///   GET  /api/flow/{correlationId}  → timeline de eventos (fallback polling 2s, AC-6)
///   POST /api/flow/{correlationId}/replay → relê a timeline e a empurra via SignalR
///                                            (anima a bolinha em tempo real, AC-6)
///
/// O serviço fica ATRÁS do gateway YARP (rota nova flow-events) — o gateway valida o
/// Bearer Entra (ADE-004/ADE-005). Este serviço não revalida o JWT.
/// </summary>
public static class FlowEndpoints
{
    public static void MapFlowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/flow");

        // AC-5 — lista das últimas compras (default 50, máx 200).
        group.MapGet("/recent", async (
            IFlowEventRepository repository,
            CancellationToken cancellationToken,
            int? top) =>
        {
            var limit = Math.Clamp(top ?? 50, 1, 200);
            var purchases = await repository.GetRecentPurchasesAsync(limit, cancellationToken);
            return Results.Ok(purchases);
        });

        // AC-6 — timeline completa (usada como fallback de polling se o WebSocket falhar).
        group.MapGet("/{correlationId}", async (
            string correlationId,
            IFlowEventRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (!IsValidCorrelationId(correlationId))
            {
                return Results.BadRequest(new { error = "correlationId inválido (esperado GUID)." });
            }

            var timeline = await repository.GetTimelineAsync(correlationId, cancellationToken);
            return Results.Ok(timeline);
        });

        // AC-6 — relê a timeline e empurra cada evento via SignalR ao grupo correlation-<id>,
        // disparando a animação da bolinha nos clientes assinantes em tempo real.
        group.MapPost("/{correlationId}/replay", async (
            string correlationId,
            IFlowEventRepository repository,
            IFlowEventPublisher publisher,
            CancellationToken cancellationToken) =>
        {
            if (!IsValidCorrelationId(correlationId))
            {
                return Results.BadRequest(new { error = "correlationId inválido (esperado GUID)." });
            }

            var timeline = await repository.GetTimelineAsync(correlationId, cancellationToken);
            foreach (var flowEvent in timeline)
            {
                await publisher.PublishAsync(flowEvent, cancellationToken);
            }

            return Results.Ok(new { correlationId, pushed = timeline.Count });
        });
    }

    /// <summary>O correlationId é sempre um GUID (gerado pelo gateway YARP — nó zero).</summary>
    private static bool IsValidCorrelationId(string value) => Guid.TryParse(value, out _);
}
