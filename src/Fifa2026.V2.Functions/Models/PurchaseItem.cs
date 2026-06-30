using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Models;

/// <summary>
/// Uma linha do carrinho (1 jogo + 1 categoria + quantidade). O envelope
/// <see cref="PurchaseRequest"/> carrega N destes. Cada item vira UMA mensagem
/// distinta no Service Bus (fan-out no Entry — Opção B), preservando o consumer
/// per-mensagem e idempotente sem alteração.
/// </summary>
public sealed class PurchaseItem
{
    [JsonPropertyName("matchId")]
    [Range(1, int.MaxValue, ErrorMessage = "matchId deve ser um inteiro positivo.")]
    public int MatchId { get; set; }

    [JsonPropertyName("category")]
    [Required(ErrorMessage = "category é obrigatório.")]
    [RegularExpression("^(VIP|Cat1|Cat2)$", ErrorMessage = "category deve ser VIP, Cat1 ou Cat2.")]
    public string Category { get; set; } = string.Empty;

    // quantity 1-10 POR LINHA (não muda o limite por compra individual).
    [JsonPropertyName("quantity")]
    [Range(1, 10, ErrorMessage = "quantity deve estar entre 1 e 10.")]
    public int Quantity { get; set; }
}
