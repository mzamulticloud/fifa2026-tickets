using Fifa2026.V2.Functions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// AC-8 — Status da compra.
/// HTTP GET /api/v2/purchase/{correlationId} → consulta `purchases` por correlation_id.
/// Se a linha ainda não foi gravada (mensagem em queue/processing), retorna status "queued".
/// </summary>
public sealed class PurchaseStatusFunction
{
    private readonly IPurchaseRepository _repository;
    private readonly ILogger<PurchaseStatusFunction> _logger;

    public PurchaseStatusFunction(IPurchaseRepository repository, ILogger<PurchaseStatusFunction> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [Function(nameof(PurchaseStatusFunction))]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v2/purchase/{correlationId}")] HttpRequest req,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(correlationId, out var parsedId))
        {
            return new BadRequestObjectResult(new { error = "correlationId deve ser um GUID válido." });
        }

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = parsedId }))
        {
            var result = await _repository.GetStatusByCorrelationIdAsync(parsedId, cancellationToken);

            if (result is null)
            {
                // Sem linha em `purchases` ainda: a mensagem está na queue ou em processamento.
                return new OkObjectResult(new { status = "queued" });
            }

            return new OkObjectResult(result);
        }
    }
}
