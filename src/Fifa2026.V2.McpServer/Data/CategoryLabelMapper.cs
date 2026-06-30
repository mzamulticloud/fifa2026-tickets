namespace Fifa2026.V2.McpServer.Data;

/// <summary>
/// Mapeia o código curto de categoria do contrato v2 (<c>VIP</c> / <c>Cat1</c> /
/// <c>Cat2</c> — ver o frontend <c>PurchaseV2Request</c> e
/// <c>Fifa2026.V2.Functions.Models.PurchaseRequest</c>) para o rótulo REAL
/// gravado na coluna <c>ticket_categories.category</c>.
///
/// FONTE DA VERDADE (não inventar): o seed real
/// <c>fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql</c> usa
/// EXATAMENTE três rótulos: <c>'VIP Premium'</c>, <c>'Categoria 1'</c>,
/// <c>'Categoria 2'</c>. As tools MCP expõem os códigos curtos; o mapeamento
/// acontece aqui, usado pelo PIVOT de
/// <see cref="FifaQueryRepository.ConsultarDisponibilidadeAsync"/>.
///
/// IMPORTANTE: esta classe é REPLICADA de forma idêntica em
/// <c>src/Fifa2026.V2.Functions/Data/CategoryLabelMapper.cs</c> porque F1
/// (Functions) e F5 (McpServer) são assemblies independentes sem projeto
/// compartilhado. Qualquer alteração nos rótulos DEVE ser feita nas duas cópias
/// e no seed real — mantê-las em sincronia é parte do contrato.
/// </summary>
internal static class CategoryLabelMapper
{
    /// <summary>Rótulo real de <c>VIP</c> no banco (seed real).</summary>
    public const string VipPremium = "VIP Premium";
    /// <summary>Rótulo real de <c>Cat1</c> no banco (seed real).</summary>
    public const string Categoria1 = "Categoria 1";
    /// <summary>Rótulo real de <c>Cat2</c> no banco (seed real).</summary>
    public const string Categoria2 = "Categoria 2";

    /// <summary>
    /// Converte o código curto do contrato v2 no rótulo real do banco.
    /// Comparação case-insensitive. Retorna <c>null</c> para códigos
    /// desconhecidos.
    /// </summary>
    public static string? ToDbLabel(string? shortCode) => shortCode?.Trim().ToUpperInvariant() switch
    {
        "VIP" => VipPremium,
        "CAT1" => Categoria1,
        "CAT2" => Categoria2,
        _ => null
    };
}
