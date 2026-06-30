using Yarp.ReverseProxy.Configuration;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// Story 2.6 / F6 (AC-3/AC-5) / ADE-003 Inv 3 — Sobrescreve a Address da destination do
/// cluster <c>flow-events</c> com a URL real do serviço FlowEvents .NET do aluno
/// (Container App), lida da configuração <c>FlowEventsUrl</c> (App Setting / env). Mesma
/// estratégia de <see cref="McpServerDestinationConfigFilter"/>: localhost em dev, URL
/// real externalizada (NUNCA hardcoded) em produção.
///
/// Mantém o gateway como NÓ ZERO do fluxo: o front fala /flow-events/** no gateway
/// (Bearer Entra), o gateway injeta o X-Correlation-ID (transform global), e o serviço
/// FlowEvents consulta a telemetria. O SignalR (WebSocket) também trafega por aqui.
/// </summary>
public sealed class FlowEventsDestinationConfigFilter : IProxyConfigFilter
{
    private const string ClusterId = "flow-events";
    private const string DestinationKey = "flow";

    private readonly string? _flowEventsUrl;

    public FlowEventsDestinationConfigFilter(IConfiguration configuration)
    {
        _flowEventsUrl = configuration["FlowEventsUrl"];
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(
        ClusterConfig cluster,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_flowEventsUrl) ||
            !string.Equals(cluster.ClusterId, ClusterId, StringComparison.OrdinalIgnoreCase) ||
            cluster.Destinations is null)
        {
            return ValueTask.FromResult(cluster);
        }

        var destinations = cluster.Destinations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == DestinationKey
                ? kvp.Value with { Address = _flowEventsUrl }
                : kvp.Value);

        return ValueTask.FromResult(cluster with { Destinations = destinations });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(route);
}
