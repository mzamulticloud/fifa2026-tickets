using Fifa2026.V2.McpServer.Data;
using Xunit;

namespace Fifa2026.V2.McpServer.Tests;

/// <summary>
/// M-1 (gate S2.5) — regressão do mismatch cross-fase no PIVOT de
/// ConsultarDisponibilidadeAsync. O PIVOT filtrava pelos códigos curtos
/// (VIP/Cat1/Cat2) mas a coluna ticket_categories.category guarda os rótulos reais
/// do seed ('VIP Premium'/'Categoria 1'/'Categoria 2', ver
/// fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql) → somas 0 e
/// preços NULL. Estes testes ASSERTAM os rótulos EXATOS do seed.
///
/// A classe é REPLICADA de forma idêntica em Functions/Data/CategoryLabelMapper.cs;
/// estes testes garantem que a cópia do McpServer continua em sincronia com o seed.
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
    [InlineData(" cat2 ", "Categoria 2")]
    public void Mapping_is_case_insensitive_and_trims(string shortCode, string expectedDbLabel)
    {
        Assert.Equal(expectedDbLabel, CategoryLabelMapper.ToDbLabel(shortCode));
    }

    [Theory]
    [InlineData("Bronze")]
    [InlineData("Categoria 1")] // rótulo do banco NÃO é código de contrato válido
    [InlineData(null)]
    public void Unknown_codes_return_null(string? shortCode)
    {
        Assert.Null(CategoryLabelMapper.ToDbLabel(shortCode));
    }

    [Fact]
    public void Constants_match_the_real_seed_labels()
    {
        Assert.Equal("VIP Premium", CategoryLabelMapper.VipPremium);
        Assert.Equal("Categoria 1", CategoryLabelMapper.Categoria1);
        Assert.Equal("Categoria 2", CategoryLabelMapper.Categoria2);
    }
}
