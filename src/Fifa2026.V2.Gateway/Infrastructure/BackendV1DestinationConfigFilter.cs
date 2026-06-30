using Yarp.ReverseProxy.Configuration;

namespace Fifa2026.V2.Gateway.Infrastructure;

/// <summary>
/// Quartas / "admin 100% workforce" (MVP) — Sobrescreve a Address da destination do
/// cluster <c>backend-v1</c> com a URL real do backend Node/Express v1 do aluno, lida
/// da configuração <c>BackendV1Url</c> (App Setting / env do Container App).
///
/// Mesma mecânica do <see cref="FunctionDestinationConfigFilter"/>: o
/// <c>appsettings.json</c> mantém <c>http://localhost:3001/</c> para desenvolvimento
/// local e a URL de produção é externalizada — NUNCA hardcoded no repo (ADE-003 Inv 3).
/// O gateway proxia as rotas admin (/admin/stats, /admin/sales, /admin/sales/{id}) para
/// este cluster, validando a policy AdminOnly e injetando X-Entra-OID + X-Gateway-Key.
/// </summary>
public sealed class BackendV1DestinationConfigFilter : IProxyConfigFilter
{
    private const string ClusterId = "backend-v1";
    private const string DestinationKey = "v1";

    private readonly string? _backendV1Url;

    public BackendV1DestinationConfigFilter(IConfiguration configuration)
    {
        _backendV1Url = configuration["BackendV1Url"];
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(
        ClusterConfig cluster,
        CancellationToken cancellationToken)
    {
        // Sem env configurada (dev local) ou cluster diferente: não altera nada.
        if (string.IsNullOrWhiteSpace(_backendV1Url) ||
            !string.Equals(cluster.ClusterId, ClusterId, StringComparison.OrdinalIgnoreCase) ||
            cluster.Destinations is null)
        {
            return ValueTask.FromResult(cluster);
        }

        var destinations = cluster.Destinations.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Key == DestinationKey
                ? kvp.Value with { Address = _backendV1Url }
                : kvp.Value);

        return ValueTask.FromResult(cluster with { Destinations = destinations });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(
        RouteConfig route,
        ClusterConfig? cluster,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(route);
}
