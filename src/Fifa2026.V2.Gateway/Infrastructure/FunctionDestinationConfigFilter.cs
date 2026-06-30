using Yarp.ReverseProxy.Configuration;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// AC-2 / ADE-003 Inv 3 — Sobrescreve a Address da destination do cluster
/// <c>functions-f1</c> com a URL real da Function App F1 do aluno, lida da
/// configuração <c>FunctionAppF1Url</c> (App Setting / env do Container App).
///
/// Mantém o <c>appsettings.json</c> com <c>http://localhost:7071/</c> para
/// desenvolvimento local e externaliza a URL de produção — a URL real da
/// Function NUNCA fica hardcoded no repo (ADE-003 Invariante 3). A connection
/// string SQL permanece nas Functions, não no gateway.
/// </summary>
public sealed class FunctionDestinationConfigFilter : IProxyConfigFilter
{
    private const string ClusterId = "functions-f1";
    private const string DestinationKey = "f1";

    private readonly string? _functionAppF1Url;

    public FunctionDestinationConfigFilter(IConfiguration configuration)
    {
        _functionAppF1Url = configuration["FunctionAppF1Url"];
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(
        ClusterConfig cluster,
        CancellationToken cancellationToken)
    {
        // Sem env configurada (dev local) ou cluster diferente: não altera nada.
        if (string.IsNullOrWhiteSpace(_functionAppF1Url) ||
            !string.Equals(cluster.ClusterId, ClusterId, StringComparison.OrdinalIgnoreCase) ||
            cluster.Destinations is null)
        {
            return ValueTask.FromResult(cluster);
        }

        var destinations = cluster.Destinations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == DestinationKey
                ? kvp.Value with { Address = _functionAppF1Url }
                : kvp.Value);

        return ValueTask.FromResult(cluster with { Destinations = destinations });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(route);
}
