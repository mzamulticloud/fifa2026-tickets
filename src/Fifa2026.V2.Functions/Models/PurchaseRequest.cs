using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Fifa2026.V2.Functions.Models;

/// <summary>
/// Envelope do POST /api/v2/purchase (carrinho inteiro).
///
/// Contrato novo (carrinho):
///   { userId, items: [ { matchId, category, quantity }, ... ] }
///
/// Contrato legado (single — smoke test depende disso), backward-compatible:
///   { matchId, category, userId, quantity }
///
/// <see cref="Normalize"/> converte o shape legado em <see cref="Items"/> com 1 linha.
/// DataAnnotations valida o envelope (userId, tamanho da coleção); cada
/// <see cref="PurchaseItem"/> é validado item-a-item no Entry (all-or-nothing) —
/// DataAnnotations NÃO desce recursivamente em coleções.
/// </summary>
public sealed class PurchaseRequest
{
    /// <summary>Máximo de linhas por compra (carrinho). Constante nomeada — nada hardcoded no fluxo.</summary>
    public const int MaxItems = 20;

    [JsonPropertyName("userId")]
    [Range(1, int.MaxValue, ErrorMessage = "userId deve ser um inteiro positivo.")]
    public int UserId { get; set; }

    [JsonPropertyName("items")]
    [Required(ErrorMessage = "items é obrigatório.")]
    [MinLength(1, ErrorMessage = "items deve conter ao menos 1 linha.")]
    [MaxLength(MaxItems, ErrorMessage = "items excede o máximo de linhas permitido por compra.")]
    public List<PurchaseItem> Items { get; set; } = new();

    // ----- Campos legados (single-item), opcionais/nullable. Só usados por Normalize(). -----

    [JsonPropertyName("matchId")]
    public int? MatchId { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    /// <summary>
    /// Se <see cref="Items"/> está vazio/nulo e os campos legados estão presentes,
    /// monta uma única linha a partir deles. Idempotente: chamar com <see cref="Items"/>
    /// já preenchido não altera nada. Não valida — apenas normaliza o shape; a validação
    /// (envelope + por item) acontece depois no Entry.
    /// </summary>
    public void Normalize()
    {
        if (Items is { Count: > 0 })
        {
            return;
        }

        if (MatchId.HasValue || Category is not null || Quantity.HasValue)
        {
            Items = new List<PurchaseItem>
            {
                new()
                {
                    MatchId = MatchId ?? 0,
                    Category = Category ?? string.Empty,
                    Quantity = Quantity ?? 0,
                },
            };
        }
    }
}
