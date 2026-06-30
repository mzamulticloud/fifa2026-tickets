using Dapper;
using Fifa2026.V2.Functions.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Data;

/// <summary>
/// Implementação Dapper + Microsoft.Data.SqlClient sobre a tabela `purchases`.
/// TODAS as queries são parametrizadas (sem concatenação de string — anti SQL injection).
/// Idempotência via UNIQUE filtered index UQ_purchases_correlation_id + INSERT-catch 2627
/// (ADE-000 Inv 4 — NUNCA SELECT-then-INSERT, evita TOCTOU race).
/// </summary>
public sealed class PurchaseRepository : IPurchaseRepository
{
    /// <summary>Código de erro do SQL Server para violação de unique/PK key.</summary>
    private const int SqlUniqueViolation = 2627;
    /// <summary>Violação de unique index (variante).</summary>
    private const int SqlDuplicateKey = 2601;

    private readonly string _connectionString;
    private readonly ILogger<PurchaseRepository> _logger;

    public PurchaseRepository(IConfiguration configuration, ILogger<PurchaseRepository> logger)
    {
        _connectionString = configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException(
                "App Setting 'SqlConnectionString' não configurado. Defina a connection string do SQL Server.");
        _logger = logger;
    }

    public async Task<InsertOutcome> InsertPurchaseAsync(PurchaseMessage message, CancellationToken cancellationToken = default)
    {
        // M-1 (gate S2.5): o contrato v2 usa códigos curtos (VIP/Cat1/Cat2), mas a
        // coluna ticket_categories.category guarda os rótulos reais do seed
        // ('VIP Premium'/'Categoria 1'/'Categoria 2' — ver
        // fifa2026-api/database/migrations/2026-05-08-real-fifa-prices.sql). Mapeamos
        // o código para o rótulo ANTES do JOIN; o contrato externo permanece curto.
        var dbCategory = CategoryLabelMapper.ToDbLabel(message.Category);
        if (dbCategory is null)
        {
            // Código de categoria desconhecido → falha permanente (mesmo tratamento
            // de categoria inexistente): o consumer encaminha ao DLQ. Não altera o
            // contrato — apenas reconhece que o código não mapeia para o seed.
            _logger.LogWarning(
                "Código de categoria desconhecido '{Category}' (correlationId={CorrelationId}). Esperado VIP/Cat1/Cat2.",
                message.Category, message.CorrelationId);
            return InsertOutcome.CategoryNotFound;
        }

        // INSERT atômico que resolve ticket_category_id, unit_price e total_price
        // a partir de ticket_categories (JOIN por match_id + category). Se o par
        // (matchId, category) não existir, o SELECT interno não retorna linha e o
        // INSERT ... SELECT não afeta nenhuma linha (rowsAffected == 0).
        // Story 2.3 AC-9 / ADE-005 Inv 3 — grava entra_oid (claim `oid` do token Entra,
        // propagado pelo gateway). NULL quando a compra não tem identidade Entra
        // (coluna entra_oid é UNIQUEIDENTIFIER NULL — alunos antigos sem oid não quebram).
        const string sql = """
            INSERT INTO dbo.purchases
                (user_id, ticket_category_id, quantity, unit_price, total_price,
                 status, source, correlation_id, entra_oid, created_at, updated_at)
            SELECT
                @UserId,
                tc.id,
                @Quantity,
                tc.price,
                tc.price * @Quantity,
                'completed',
                'v2',
                @CorrelationId,
                @EntraOid,
                GETDATE(),
                GETDATE()
            FROM dbo.ticket_categories tc
            WHERE tc.match_id = @MatchId
              AND tc.category = @Category;
            """;

        await using var connection = new SqlConnection(_connectionString);

        try
        {
            var command = new CommandDefinition(
                sql,
                new
                {
                    message.UserId,
                    message.Quantity,
                    message.CorrelationId,
                    message.MatchId,
                    Category = dbCategory,
                    message.EntraOid
                },
                cancellationToken: cancellationToken);

            var rowsAffected = await connection.ExecuteAsync(command);

            if (rowsAffected == 0)
            {
                // Nenhuma categoria casou (matchId/category inválidos) → não é duplicata.
                // O consumer trata isso como falha permanente (vai para DLQ).
                _logger.LogWarning(
                    "Nenhuma ticket_category para matchId={MatchId} category={Category} (correlationId={CorrelationId}).",
                    message.MatchId, message.Category, message.CorrelationId);
                return InsertOutcome.CategoryNotFound;
            }

            return InsertOutcome.Inserted;
        }
        catch (SqlException ex) when (ex.Number is SqlUniqueViolation or SqlDuplicateKey)
        {
            // Idempotência atingida: a mesma mensagem já foi processada (at-least-once
            // delivery do Service Bus). Tratar como sucesso silencioso (ADE-000 Inv 4).
            _logger.LogInformation(
                "Duplicata ignorada (idempotência) para correlationId={CorrelationId}.",
                message.CorrelationId);
            return InsertOutcome.Duplicate;
        }
    }

    public async Task<PurchaseStatusResult?> GetStatusByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (1) id AS Id, status AS Status
            FROM dbo.purchases
            WHERE correlation_id = @CorrelationId
            ORDER BY id DESC;
            """;

        await using var connection = new SqlConnection(_connectionString);

        var command = new CommandDefinition(
            sql,
            new { CorrelationId = correlationId },
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<PurchaseRow>(command);

        if (row is null)
        {
            return null;
        }

        return new PurchaseStatusResult
        {
            Status = MapStatus(row.Status),
            TicketId = string.Equals(row.Status, "completed", StringComparison.OrdinalIgnoreCase)
                ? row.Id
                : null
        };
    }

    /// <summary>Mapeia o status do DB para o vocabulário do contrato v2.</summary>
    private static string MapStatus(string? dbStatus) => dbStatus?.ToLowerInvariant() switch
    {
        "completed" => "completed",
        "failed" => "failed",
        "processing" => "processing",
        _ => "processing"
    };

    private sealed record PurchaseRow(int Id, string? Status);
}
