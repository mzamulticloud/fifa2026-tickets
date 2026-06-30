using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Models;

/// <summary>
/// Mensagem publicada em `tickets-purchase` pela PurchaseEntryFunction e consumida
/// pela PurchaseConsumerFunction. Carrega o correlationId gerado na entrada para
/// propagação ponta-a-ponta (ADE-000 Inv 5 — Service Bus hop).
/// </summary>
public sealed class PurchaseMessage
{
    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Identificador do pedido (carrinho) que originou esta mensagem. Várias mensagens
    /// (1 por linha do carrinho) compartilham o mesmo OrderId, cada uma com seu
    /// CorrelationId distinto. Nullable: mensagens legadas (pré-multi-item) não o trazem,
    /// e o consumer não depende dele — não quebra o contrato existente.
    /// </summary>
    [JsonPropertyName("orderId")]
    public Guid? OrderId { get; set; }

    [JsonPropertyName("matchId")]
    public int MatchId { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Story 2.3 AC-9 / ADE-005 Inv 3/4 — Object ID (GUID) do usuário no tenant Entra
    /// workforce, claim `oid` extraído e propagado pelo gateway YARP via header
    /// X-Entra-OID. NULL para fluxos sem identidade Entra (ex.: F1/F2 anônimo, ou
    /// alunos antigos antes de F3). A Function NÃO valida o token — confia no
    /// gateway como guardião único (ADE-005 Inv 4).
    /// </summary>
    [JsonPropertyName("entraOid")]
    public Guid? EntraOid { get; set; }
}
