using Fifa2026.V2.Functions.Models;

namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Abstração de acesso a dados sobre a tabela `purchases` (mesma DB do v1).
/// Mantida como interface para permitir mock nos testes unitários (AC-8 testing).
/// </summary>
public interface IPurchaseRepository
{
    /// <summary>
    /// Insere uma compra v2 de forma idempotente (ADE-000 Inv 4).
    /// Resolve unit_price/ticket_category_id via JOIN em ticket_categories
    /// (match_id + category) e grava source='v2', status='completed'.
    /// </summary>
    /// <returns>
    /// <see cref="InsertOutcome.Inserted"/> em inserção nova;
    /// <see cref="InsertOutcome.Duplicate"/> se correlation_id já existe (SqlException 2627);
    /// <see cref="InsertOutcome.CategoryNotFound"/> se o par (matchId, category) não existe.
    /// </returns>
    Task<InsertOutcome> InsertPurchaseAsync(PurchaseMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta o status de uma compra pelo correlation_id (AC-8).
    /// Retorna null se nenhuma linha correspondente existir (ainda em queue/processing).
    /// </summary>
    Task<PurchaseStatusResult?> GetStatusByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de uma tentativa de inserção idempotente.</summary>
public enum InsertOutcome
{
    Inserted,
    Duplicate,
    CategoryNotFound
}
