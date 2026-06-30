using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Fifa2026.V2.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fifa2026.V2.Functions.Functions;

/// <summary>
/// AC-3 — Entrada do fluxo de compra v2 (carrinho inteiro — Opção B / fan-out no Entry).
/// HTTP POST /api/v2/purchase → 1 orderId (GUID) + N correlationIds (1 por linha do
/// carrinho) → publica N mensagens em `tickets-purchase` via output binding declarativo
/// (string[] → N mensagens distintas) → responde 202 unificada
/// { orderId, status:"queued", correlationIds[], correlationId? }.
/// O consumer NÃO muda: continua per-mensagem e idempotente.
/// authLevel Anonymous em F1 (segurança entra na F2 com gateway — blueprint troubleshooting).
/// </summary>
public sealed class PurchaseEntryFunction
{
    /// <summary>Nome da fila no Service Bus. Constante nomeada — nada hardcoded.</summary>
    private const string QueueName = "tickets-purchase";

    /// <summary>
    /// Story 2.3 AC-9 / ADE-005 Inv 4 — header de identidade propagado pelo gateway
    /// YARP após validar o JWT Entra (claim `oid`). A Function confia neste header
    /// (não valida token) porque o gateway é o guardião único. O cliente nunca chama
    /// a Function diretamente (URL real não exposta — ADE-004 Inv 1/5).
    /// </summary>
    private const string EntraOidHeader = "X-Entra-OID";

    private readonly ILogger<PurchaseEntryFunction> _logger;

    public PurchaseEntryFunction(ILogger<PurchaseEntryFunction> logger)
    {
        _logger = logger;
    }

    /// <summary>Saída do binding HTTP + N mensagens para o Service Bus (fan-out).</summary>
    public sealed class EntryOutput
    {
        // SEM EntityPath na connection — o nome da queue vem deste atributo (blueprint troubleshooting).
        // string[] no worker isolado → o host publica UMA mensagem distinta por elemento do array.
        // Em caminhos de erro (400) deixamos Messages = null/empty → NADA é publicado (all-or-nothing).
        [ServiceBusOutput(QueueName, Connection = "ServiceBusConnection")]
        public string[]? Messages { get; set; }

        // [HttpResult] é OBRIGATÓRIO em multi-output binding com integração ASP.NET Core
        // (ConfigureFunctionsWebApplication): sem ele, o host não escreve o IActionResult
        // na resposta e devolve 200 vazio (o output do Service Bus continua funcionando).
        [HttpResult]
        public IActionResult? HttpResponse { get; set; }
    }

    [Function(nameof(PurchaseEntryFunction))]
    public async Task<EntryOutput> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v2/purchase")] HttpRequest req)
    {
        PurchaseRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PurchaseRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Body JSON inválido em POST /api/v2/purchase.");
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "JSON inválido." })
            };
        }

        if (request is null)
        {
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "Body obrigatório." })
            };
        }

        // Aceita os dois shapes: legado single → vira items[0]; carrinho → mantém items.
        request.Normalize();

        // Valida o ENVELOPE (userId, items presente e dentro de [1, MaxItems]).
        // DataAnnotations NÃO desce em coleções, então cada item é validado no loop abaixo.
        var envelopeContext = new ValidationContext(request);
        var envelopeResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, envelopeContext, envelopeResults, validateAllProperties: true))
        {
            var errors = envelopeResults.ConvertAll(r => r.ErrorMessage);
            _logger.LogWarning("Validação do envelope falhou em POST /api/v2/purchase: {Errors}", string.Join("; ", errors));
            return new EntryOutput
            {
                HttpResponse = new BadRequestObjectResult(new { error = "Validação falhou.", details = errors })
            };
        }

        // all-or-nothing: valida TODOS os itens ANTES de publicar QUALQUER mensagem.
        var itemErrors = new List<string?>();
        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            var itemContext = new ValidationContext(item);
            var itemResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(item, itemContext, itemResults, validateAllProperties: true))
            {
                itemErrors.AddRange(itemResults.ConvertAll(r => $"items[{i}]: {r.ErrorMessage}"));
            }
        }

        if (itemErrors.Count > 0)
        {
            _logger.LogWarning("Validação de itens falhou em POST /api/v2/purchase: {Errors}", string.Join("; ", itemErrors));
            return new EntryOutput
            {
                // Messages permanece null → NADA publicado.
                HttpResponse = new BadRequestObjectResult(new { error = "Validação falhou.", details = itemErrors })
            };
        }

        var orderId = Guid.NewGuid();

        // Story 2.3 AC-9 — lê o X-Entra-OID UMA vez (propagado pelo gateway, claim `oid`).
        // Ausente/inválido → null (fluxo segue sem identidade Entra; entra_oid NULL no SQL).
        // Replicado em TODAS as mensagens do carrinho. NÃO logamos o oid (PII — AC-12).
        Guid? entraOid = null;
        var entraOidHeader = req.Headers[EntraOidHeader].ToString();
        if (!string.IsNullOrWhiteSpace(entraOidHeader) && Guid.TryParse(entraOidHeader, out var parsedOid))
        {
            entraOid = parsedOid;
        }

        var correlationIds = new List<Guid>(request.Items.Count);
        var messages = new string[request.Items.Count];

        // BeginScope com orderId → App Insights agrupa todas as linhas do pedido (ADE-000 Inv 5).
        using (_logger.BeginScope(new Dictionary<string, object> { ["OrderId"] = orderId }))
        {
            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                var correlationId = Guid.NewGuid();
                correlationIds.Add(correlationId);

                _logger.LogInformation(
                    "Compra v2 (linha {Index}/{Total}): correlationId={CorrelationId} matchId={MatchId} category={Category} userId={UserId} quantity={Quantity} hasEntraIdentity={HasEntraIdentity}",
                    i + 1, request.Items.Count, correlationId, item.MatchId, item.Category, request.UserId, item.Quantity, entraOid.HasValue);

                var message = new PurchaseMessage
                {
                    CorrelationId = correlationId,
                    OrderId = orderId,
                    MatchId = item.MatchId,
                    Category = item.Category,
                    UserId = request.UserId,
                    Quantity = item.Quantity,
                    EntraOid = entraOid
                };

                messages[i] = JsonSerializer.Serialize(message);
            }

            // correlationId (singular) presente APENAS quando há exatamente 1 item
            // (== correlationIds[0]). É o campo que o smoke `jq -e '.correlationId'` lê.
            // Tipado como Guid? para o System.Text.Json emitir o GUID quando count==1
            // e omitir/null quando multi-item (sem fabricar valor).
            Guid? singleCorrelationId = correlationIds.Count == 1 ? correlationIds[0] : null;

            return new EntryOutput
            {
                Messages = messages,
                HttpResponse = new AcceptedResult(
                    location: $"/api/v2/purchase/{correlationIds[0]}",
                    value: new
                    {
                        orderId,
                        status = "queued",
                        correlationIds,
                        correlationId = singleCorrelationId
                    })
            };
        }
    }
}
