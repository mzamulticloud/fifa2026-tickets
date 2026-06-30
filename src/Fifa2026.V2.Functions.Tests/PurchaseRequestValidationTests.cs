using System.ComponentModel.DataAnnotations;
using Fifa2026.V2.Functions.Models;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Validação do envelope <see cref="PurchaseRequest"/> (userId, items 1..MaxItems),
/// de cada <see cref="PurchaseItem"/> (matchId, category, quantity 1-10) e do
/// <see cref="PurchaseRequest.Normalize"/> (shape legado single → 1 item).
///
/// Nota: DataAnnotations NÃO valida recursivamente a coleção — os itens são validados
/// individualmente (como faz o Entry no loop all-or-nothing).
/// </summary>
public sealed class PurchaseRequestValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object target)
    {
        var context = new ValidationContext(target);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(target, context, results, validateAllProperties: true);
        return results;
    }

    private static PurchaseItem Item(int matchId = 1, string category = "VIP", int quantity = 1) =>
        new() { MatchId = matchId, Category = category, Quantity = quantity };

    private static PurchaseRequest Envelope(int userId = 5, params PurchaseItem[] items) =>
        new() { UserId = userId, Items = items.ToList() };

    // ----------------------------- Envelope -----------------------------

    [Fact]
    public void Valid_envelope_passes_validation()
    {
        Assert.Empty(Validate(Envelope(5, Item())));
    }

    [Fact]
    public void Empty_items_fails_MinLength()
    {
        var request = new PurchaseRequest { UserId = 5, Items = new List<PurchaseItem>() };

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void Items_above_MaxItems_fails_MaxLength()
    {
        var items = Enumerable.Range(0, PurchaseRequest.MaxItems + 1).Select(_ => Item()).ToArray();

        Assert.NotEmpty(Validate(Envelope(5, items)));
    }

    [Fact]
    public void Items_at_MaxItems_passes()
    {
        var items = Enumerable.Range(0, PurchaseRequest.MaxItems).Select(_ => Item()).ToArray();

        Assert.Empty(Validate(Envelope(5, items)));
    }

    [Fact]
    public void Non_positive_userId_fails()
    {
        Assert.NotEmpty(Validate(Envelope(0, Item())));
    }

    // ----------------------------- Item -----------------------------

    [Theory]
    [InlineData("VIP")]
    [InlineData("Cat1")]
    [InlineData("Cat2")]
    public void Allowed_categories_pass(string category)
    {
        Assert.Empty(Validate(Item(category: category)));
    }

    [Theory]
    [InlineData("vip")]
    [InlineData("Cat3")]
    [InlineData("")]
    [InlineData("VVIP")]
    public void Invalid_category_fails(string category)
    {
        Assert.NotEmpty(Validate(Item(category: category)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_matchId_fails(int matchId)
    {
        Assert.NotEmpty(Validate(Item(matchId: matchId)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Quantity_in_range_passes(int quantity)
    {
        Assert.Empty(Validate(Item(quantity: quantity)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Quantity_out_of_range_fails(int quantity)
    {
        Assert.NotEmpty(Validate(Item(quantity: quantity)));
    }

    // ----------------------------- Normalize (shape legado) -----------------------------

    [Fact]
    public void Normalize_legacy_single_builds_one_item()
    {
        var request = new PurchaseRequest
        {
            UserId = 7,
            MatchId = 3,
            Category = "Cat2",
            Quantity = 4,
        };

        request.Normalize();

        var item = Assert.Single(request.Items);
        Assert.Equal(3, item.MatchId);
        Assert.Equal("Cat2", item.Category);
        Assert.Equal(4, item.Quantity);
        // Envelope normalizado deve passar na validação.
        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Normalize_is_noop_when_items_already_present()
    {
        var request = new PurchaseRequest
        {
            UserId = 7,
            Items = new List<PurchaseItem> { Item(matchId: 1, category: "VIP", quantity: 2) },
            // Campos legados presentes NÃO devem sobrepor o carrinho já montado.
            MatchId = 99,
            Category = "Cat1",
            Quantity = 9,
        };

        request.Normalize();

        var item = Assert.Single(request.Items);
        Assert.Equal(1, item.MatchId);
        Assert.Equal("VIP", item.Category);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Normalize_leaves_items_empty_when_no_legacy_fields()
    {
        var request = new PurchaseRequest { UserId = 7 };

        request.Normalize();

        Assert.Empty(request.Items);
        // Sem itens e sem legado → envelope inválido (MinLength).
        Assert.NotEmpty(Validate(request));
    }
}
