using Fifa2026.V2.Functions.Data;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// M-1 (gate S2.5) — regressão do mismatch cross-fase: o contrato v2 usa códigos
/// curtos (VIP/Cat1/Cat2) mas a coluna ticket_categories.category guarda os rótulos
/// reais do seed ('VIP Premium'/'Categoria 1'/'Categoria 2', ver
/// fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql).
///
/// Estes testes ASSERTAM os rótulos EXATOS do seed — teriam pego o bug original
/// (JOIN por 'VIP' nunca casava 'VIP Premium' → CategoryNotFound → DLQ).
/// </summary>
public sealed class CategoryLabelMapperTests
{
    [Theory]
    [InlineData("VIP", "VIP Premium")]
    [InlineData("Cat1", "Categoria 1")]
    [InlineData("Cat2", "Categoria 2")]
    public void Maps_short_code_to_exact_seed_label(string shortCode, string expectedDbLabel)
    {
        Assert.Equal(expectedDbLabel, CategoryLabelMapper.ToDbLabel(shortCode));
    }

    [Theory]
    [InlineData("vip", "VIP Premium")]
    [InlineData("cat1", "Categoria 1")]
    [InlineData("  CAT2  ", "Categoria 2")]
    public void Mapping_is_case_insensitive_and_trims(string shortCode, string expectedDbLabel)
    {
        Assert.Equal(expectedDbLabel, CategoryLabelMapper.ToDbLabel(shortCode));
    }

    [Theory]
    [InlineData("VIP Premium")] // o rótulo do banco NÃO é um código de contrato válido
    [InlineData("Categoria 1")]
    [InlineData("Bronze")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_codes_return_null(string? shortCode)
    {
        Assert.Null(CategoryLabelMapper.ToDbLabel(shortCode));
    }

    [Fact]
    public void Constants_match_the_real_seed_labels()
    {
        // Trava os rótulos contra o seed real — se alguém mudar uma constante sem
        // mudar o seed (ou vice-versa), este teste falha.
        Assert.Equal("VIP Premium", CategoryLabelMapper.VipPremium);
        Assert.Equal("Categoria 1", CategoryLabelMapper.Categoria1);
        Assert.Equal("Categoria 2", CategoryLabelMapper.Categoria2);
    }
}
