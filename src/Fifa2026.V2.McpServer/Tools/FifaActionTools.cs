using System.ComponentModel;
using Fifa2026.V2.McpServer.Data;
using ModelContextProtocol.Server;

namespace Fifa2026.V2.McpServer.Tools;

/// <summary>
/// Story 2.9 (Fase B / ADE-006) — a primeira "MÃO" do chatbot: tools de AÇÃO
/// (<c>ReadOnly=false</c>), separadas das tools de leitura ("sentidos") de
/// <see cref="FifaTicketTools"/> para materializar didaticamente o discriminador
/// estrutural da ADE-006 Inv 2.
///
/// REGRA DE OURO (ADE-006 Inv 1): mãos NUNCA escrevem no SQL — agem exclusivamente
/// disparando a orquestração no n8n (webhook fire-and-forget). A decisão de COMO
/// executar fica no AI Agent do n8n (Agente 2); o front (Gemini) decide O QUÊ.
/// </summary>
[McpServerToolType]
public static class FifaActionTools
{
    // ReadOnly OMITIDO de propósito (default do SDK = false) — esta é a primeira mão.
    [McpServerTool(Name = "criar_alerta_ingresso")]
    [Description(
        "Cria um alerta para avisar quando ingressos ficarem disponíveis para uma partida. " +
        "Aciona uma automação de orquestração no n8n. Use quando o usuário pedir para ser " +
        "avisado ou monitorar disponibilidade de ingresso para um jogo.")]
    public static async Task<AlertResult> CriarAlertaIngressoAsync(
        IAlertWebhookNotifier alertNotifier,
        EntraOidContext oidContext,
        ILogger<FifaTicketTools.DiagnosticsCategory> logger,
        [Description("ID numérico da partida (opcional se matchDescription for informado).")]
        int? matchId = null,
        [Description("Descrição da partida, ex.: 'final', 'Brasil x Argentina' (opcional se matchId for informado).")]
        string? matchDescription = null,
        [Description("Categoria do ingresso desejada: 'VIP', 'Cat1' ou 'Cat2' (opcional).")]
        string? categoria = null,
        CancellationToken cancellationToken = default)
    {
        // Pelo menos um identificador de partida é obrigatório (AC-2) — sem ele não
        // há o que monitorar; retorna erro descritivo SEM acionar o notifier.
        if (matchId is null && string.IsNullOrWhiteSpace(matchDescription))
        {
            return new AlertResult
            {
                Registrado = false,
                Mensagem = "Informe matchId ou matchDescription para criar o alerta."
            };
        }

        // AC-3 graceful degradation: ambiente sem N8N_ALERT_WEBHOOK_URL responde com
        // mensagem específica (não confundir com falha transitória do webhook).
        if (!alertNotifier.IsConfigured)
        {
            return new AlertResult
            {
                Registrado = false,
                Mensagem = "Webhook de alerta não configurado neste ambiente."
            };
        }

        // Log SEMPRE mascarado — o oid raw alimenta apenas o payload (ADE-006 Inv 7;
        // Task 9.4 audita que GetRawOid() nunca aparece em log).
        logger.LogInformation(
            "tool=criar_alerta_ingresso oid={Oid} matchId={MatchId} matchDescription={Desc}",
            oidContext.GetMaskedOidForLog(), matchId, matchDescription);

        var payload = new AlertWebhookPayload
        {
            // Novo GUID por disparo — contexto de rastreabilidade do ALERTA (futuro F6),
            // distinto do X-Correlation-ID da request HTTP injetado pelo gateway (Gotcha 7).
            CorrelationId = Guid.NewGuid(),
            EntraOid = oidContext.GetRawOid(),
            MatchId = matchId,
            MatchDescription = matchDescription,
            // Rótulo real do banco ou null — nunca rótulo inventado (anti-alucinação).
            Categoria = CategoryLabelMapper.ToDbLabel(categoria),
            RequestedAt = DateTimeOffset.UtcNow
        };

        var ok = await alertNotifier.NotifyAlertAsync(payload, cancellationToken);
        return ok
            ? new AlertResult
            {
                Registrado = true,
                Mensagem = "Alerta registrado. Você será notificado quando houver disponibilidade."
            }
            : new AlertResult
            {
                Registrado = false,
                Mensagem = "Não foi possível registrar o alerta. Tente novamente mais tarde."
            };
    }
}
