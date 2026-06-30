namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Mapeia o código curto de categoria do contrato v2 (<c>VIP</c> / <c>Cat1</c> /
/// <c>Cat2</c> — ver <see cref="Models.PurchaseRequest"/> e o frontend
/// <c>PurchaseV2Request</c>) para o rótulo REAL gravado na coluna
/// <c>ticket_categories.category</c>.
///
/// FONTE DA VERDADE (não inventar): o seed real
/// <c>fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql</c> usa
/// EXATAMENTE três rótulos: <c>'VIP Premium'</c>, <c>'Categoria 1'</c>,
/// <c>'Categoria 2'</c>. O contrato externo (API/mensagem do Service Bus)
/// permanece com os códigos curtos; o mapeamento acontece aqui, imediatamente
/// antes do JOIN em <c>ticket_categories</c> (ver
/// <see cref="PurchaseRepository.InsertPurchaseAsync"/>).
///
/// IMPORTANTE: esta classe é REPLICADA de forma idêntica em
/// <c>src/Fifa2026.V2.McpServer/Data/CategoryLabelMapper.cs</c> porque F1
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
    /// desconhecidos — o chamador trata isso como categoria inexistente
    /// (no consumer F1, leva a <see cref="InsertOutcome.CategoryNotFound"/> → DLQ),
    /// preservando o comportamento de falha permanente sem alterar o contrato.
    /// </summary>
    public static string? ToDbLabel(string? shortCode) => shortCode?.Trim().ToUpperInvariant() switch
    {
        "VIP" => VipPremium,
        "CAT1" => Categoria1,
        "CAT2" => Categoria2,
        _ => null
    };
}
