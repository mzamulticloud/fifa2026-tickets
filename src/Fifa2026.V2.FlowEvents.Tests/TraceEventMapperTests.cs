using Fifa2026.V2.FlowEvents.Data;
using Fifa2026.V2.FlowEvents.Models;
using Xunit;

namespace Fifa2026.V2.FlowEvents.Tests;

/// <summary>
/// AC-4/AC-13 — classificação de traces do App Insights nos 6 hops REAIS.
/// O NÓ ZERO é o Gateway YARP — NUNCA APIM. Estes testes são a barreira
/// anti-regressão contra a narrativa antiga (APIM como nó zero).
/// </summary>
public sealed class TraceEventMapperTests
{
    [Theory]
    [InlineData("fifa2026-gateway", "X-Correlation-ID injetado", FlowEventType.GATEWAY_YARP_RECEIVED)]
    [InlineData("yarp-gateway", "request recebida", FlowEventType.GATEWAY_YARP_RECEIVED)]
    [InlineData("fifa2026-functions", "Compra v2 recebida: matchId=1", FlowEventType.FUNCTION_ENTRY_PROCESSED)]
    [InlineData("fifa2026-functions", "Mensagem publicada em tickets-purchase", FlowEventType.SERVICE_BUS_PUBLISHED)]
    [InlineData("fifa2026-functions", "Processando compra v2: matchId=1", FlowEventType.FUNCTION_CONSUMER_DONE)]
    [InlineData("fifa2026-functions", "Falha no disparo do webhook n8n", FlowEventType.N8N_WEBHOOK_TRIGGERED)]
    [InlineData("fifa2026-functions", "Compra v2 gravada com sucesso", FlowEventType.SQL_INSERTED)]
    public void Classify_maps_real_hops(string role, string message, FlowEventType expected)
    {
        Assert.Equal(expected, TraceEventMapper.Classify(role, message));
    }

    [Fact]
    public void Node_zero_is_gateway_yarp_never_apim()
    {
        // AC-13 — a arquitetura real NÃO tem APIM. Um trace "apim" não deve mapear ao nó 0
        // (nem a nenhum hop), garantindo que a narrativa antiga não ressuscite.
        var result = TraceEventMapper.Classify("legacy-apim", "APIM policy executed");
        Assert.Null(result);

        // E o gateway YARP é, sim, o nó 0 (ordinal 0).
        Assert.Equal(0, (int)FlowEventType.GATEWAY_YARP_RECEIVED);
    }

    [Fact]
    public void Unknown_trace_returns_null()
    {
        Assert.Null(TraceEventMapper.Classify("fifa2026-gateway-healthprobe", "GET /health 200"));
        Assert.Null(TraceEventMapper.Classify(null, null));
    }

    [Theory]
    [InlineData(0, "ok")]
    [InlineData(1, "ok")]
    [InlineData(2, "ok")]
    [InlineData(3, "error")]
    [InlineData(4, "error")]
    public void StatusFromSeverity_marks_error_at_3_or_above(int severity, string expected)
    {
        Assert.Equal(expected, TraceEventMapper.StatusFromSeverity(severity));
    }

    [Fact]
    public void All_six_event_types_have_distinct_sequential_node_indexes()
    {
        // Garante 6 nós, ordinais 0..5 sem buracos (a animação da bolinha depende disso).
        var indexes = Enum.GetValues<FlowEventType>().Select(t => (int)t).OrderBy(i => i).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, indexes);
    }
}
