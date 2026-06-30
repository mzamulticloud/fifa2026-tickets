using Yarp.ReverseProxy.Configuration;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// Story 2.5 / F5 (AC-8) / ADE-003 Inv 3 — Sobrescreve a Address da destination do
/// cluster <c>mcp-server</c> com a URL real do McpServer .NET do aluno (Container
/// App), lida da configuração <c>McpServerUrl</c> (App Setting / env). Mesma
/// estratégia de <see cref="FunctionDestinationConfigFilter"/>: localhost em dev,
/// URL real externalizada (NUNCA hardcoded) em produção.
///
/// Mantém o gateway como ponto único de auth: o chatbot fala /mcp e /llm no gateway
/// (Bearer Entra), e o gateway propaga X-Entra-OID ao McpServer (ADE-005 Inv 4).
/// </summary>
public sealed class McpServerDestinationConfigFilter : IProxyConfigFilter
{
    private const string ClusterId = "mcp-server";
    private const string DestinationKey = "mcp";

    private readonly string? _mcpServerUrl;

    public McpServerDestinationConfigFilter(IConfiguration configuration)
    {
        _mcpServerUrl = configuration["McpServerUrl"];
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(
        ClusterConfig cluster,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_mcpServerUrl) ||
            !string.Equals(cluster.ClusterId, ClusterId, StringComparison.OrdinalIgnoreCase) ||
            cluster.Destinations is null)
        {
            return ValueTask.FromResult(cluster);
        }

        var destinations = cluster.Destinations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == DestinationKey
                ? kvp.Value with { Address = _mcpServerUrl }
                : kvp.Value);

        return ValueTask.FromResult(cluster with { Destinations = destinations });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(route);
}
