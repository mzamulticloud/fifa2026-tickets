using Fifa2026.V2.FlowEvents.Models;

namespace Fifa2026.V2.FlowEvents.Data;

/// <summary>
/// AC-4/AC-13 — mapeia um trace bruto do App Insights (operação/mensagem/origem) para
/// um <see cref="FlowEventType"/> tipado, refletindo a arquitetura REAL implementada
/// nas fases anteriores. O NÓ ZERO é o Gateway YARP (ADE-004) — NUNCA APIM.
///
/// Lógica de classificação isolada e PURA (sem I/O) para ser 100% unit-testável sem
/// um workspace App Insights vivo (Story 2.6 Testing approach — mock App Insights).
/// </summary>
internal static class TraceEventMapper
{
    /// <summary>
    /// Classifica um trace pelo nome da operação/role do componente e pelo conteúdo da
    /// mensagem. Retorna null se o trace não corresponder a nenhum dos 6 hops conhecidos
    /// (ex.: ruído de telemetria de health probe).
    /// </summary>
    /// <param name="cloudRoleName">customDimensions/cloud_RoleName do componente emissor.</param>
    /// <param name="message">texto do trace (LogInformation).</param>
    internal static FlowEventType? Classify(string? cloudRoleName, string? message)
    {
        var role = (cloudRoleName ?? string.Empty).ToLowerInvariant();
        var msg = (message ?? string.Empty).ToLowerInvariant();

        // Ruído de health probe NÃO é um hop do fluxo (não carrega correlationId real).
        // Descartado antes de qualquer match de role (senão um probe do gateway cairia no nó 0).
        if (msg.Contains("/health") || role.Contains("healthprobe") || role.Contains("health-probe"))
        {
            return null;
        }

        // Nó 0 — Gateway YARP (injeta X-Correlation-ID; nó zero). NÃO usa "apim".
        // Exige o sinal de correlação (mensagem do transform) OU role gateway/yarp +
        // ausência de marcadores de hops posteriores — o gateway é a borda do fluxo.
        if (msg.Contains("x-correlation-id") || msg.Contains("correlation-id injetado")
            || (role.Contains("gateway") && (msg.Contains("recebid") || msg.Contains("request")))
            || (role.Contains("yarp") && (msg.Contains("recebid") || msg.Contains("request"))))
        {
            return FlowEventType.GATEWAY_YARP_RECEIVED;
        }

        // Nó 1 — PurchaseEntryFunction: "Compra v2 recebida" (mensagem real da F1).
        if (msg.Contains("compra v2 recebida"))
        {
            return FlowEventType.FUNCTION_ENTRY_PROCESSED;
        }

        // Nó 2 — Service Bus publish (mensagem enfileirada em tickets-purchase).
        if (msg.Contains("tickets-purchase") && (msg.Contains("publish") || msg.Contains("publicad")))
        {
            return FlowEventType.SERVICE_BUS_PUBLISHED;
        }

        // Nó 4 — webhook do n8n (avaliado antes do consumer pois "n8n" é mais específico).
        if (msg.Contains("webhook n8n") || msg.Contains("n8n") || role.Contains("n8n"))
        {
            return FlowEventType.N8N_WEBHOOK_TRIGGERED;
        }

        // Nó 5 — gravação no SQL (mensagem real do consumer ao inserir).
        if (msg.Contains("gravada com sucesso") || msg.Contains("sql_inserted") || msg.Contains("purchases.correlation_id"))
        {
            return FlowEventType.SQL_INSERTED;
        }

        // Nó 3 — PurchaseConsumerFunction: "Processando compra v2" (mensagem real da F1 consumer).
        if (msg.Contains("processando compra v2"))
        {
            return FlowEventType.FUNCTION_CONSUMER_DONE;
        }

        return null;
    }

    /// <summary>
    /// Deriva o status do hop a partir do severityLevel do trace (App Insights:
    /// 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical). >= 3 → "error".
    /// </summary>
    internal static string StatusFromSeverity(int severityLevel) => severityLevel >= 3 ? "error" : "ok";
}
