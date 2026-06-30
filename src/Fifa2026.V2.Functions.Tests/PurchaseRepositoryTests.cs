using Fifa2026.V2.Functions.Data;
using Fifa2026.V2.Functions.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// M-1 (gate S2.5) — fronteira do PurchaseRepository ANTES de qualquer acesso ao
/// banco. Quando o código de categoria não mapeia para um rótulo do seed
/// (CategoryLabelMapper.ToDbLabel == null), o repositório retorna
/// <see cref="InsertOutcome.CategoryNotFound"/> em curto-circuito — sem abrir
/// conexão SQL. Isso permite testar o guard sem um banco real (mission: NÃO tocar
/// no banco). O caminho de código VÁLIDO faz acesso a SQL e é coberto por testes
/// de integração fora deste projeto unitário.
/// </summary>
public sealed class PurchaseRepositoryTests
{
    private static PurchaseRepository Build()
    {
        // Connection string fake — o teste de código inválido NUNCA chega a abri-la.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlConnectionString"] = "Server=(local);Database=fake;Trusted_Connection=True;"
            })
            .Build();

        return new PurchaseRepository(config, NullLogger<PurchaseRepository>.Instance);
    }

    [Theory]
    [InlineData("Bronze")]
    [InlineData("VIP Premium")] // rótulo do banco NÃO é código de contrato válido
    [InlineData("")]
    public async Task Unknown_category_code_returns_CategoryNotFound_without_db(string badCategory)
    {
        var repo = Build();
        var message = new PurchaseMessage
        {
            CorrelationId = Guid.NewGuid(),
            MatchId = 1,
            Category = badCategory,
            UserId = 7,
            Quantity = 1
        };

        var outcome = await repo.InsertPurchaseAsync(message, CancellationToken.None);

        Assert.Equal(InsertOutcome.CategoryNotFound, outcome);
    }
}
