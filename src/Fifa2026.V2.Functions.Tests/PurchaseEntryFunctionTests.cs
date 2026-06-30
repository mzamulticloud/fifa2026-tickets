using System.Text;
using System.Text.Json;
using Fifa2026.V2.Functions.Functions;
using Fifa2026.V2.Functions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// PurchaseEntryFunction — fan-out do carrinho (Opção B) + Story 2.3 AC-9.
///
/// A Function explode o carrinho em N mensagens (1 por linha) no Service Bus, todas
/// com o mesmo orderId e correlationIds distintos, lendo o X-Entra-OID UMA vez e
/// replicando-o. Regressão: shape legado single continua respondendo 202 com
/// `correlationId` singular (== correlationIds[0]), o que o smoke test do workflow lê.
/// </summary>
public sealed class PurchaseEntryFunctionTests
{
    private static HttpRequest BuildRequest(object body, string? entraOidHeader = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var json = JsonSerializer.Serialize(body);
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        request.ContentType = "application/json";
        request.ContentLength = json.Length;
        if (entraOidHeader is not null)
        {
            request.Headers["X-Entra-OID"] = entraOidHeader;
        }
        return request;
    }

    private static PurchaseMessage? DeserializeMessage(string message) =>
        JsonSerializer.Deserialize<PurchaseMessage>(
            message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static IReadOnlyList<PurchaseMessage> DeserializeAll(string[]? messages)
    {
        Assert.NotNull(messages);
        return messages!.Select(m =>
        {
            var msg = DeserializeMessage(m);
            Assert.NotNull(msg);
            return msg!;
        }).ToList();
    }

    /// <summary>Lê o campo `correlationId` (singular) do value anônimo da resposta 202.</summary>
    private static Guid? ReadSingularCorrelationId(AcceptedResult accepted)
    {
        var json = JsonSerializer.Serialize(accepted.Value);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("correlationId", out var prop) ||
            prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return prop.GetGuid();
    }

    private static IReadOnlyList<Guid> ReadCorrelationIds(AcceptedResult accepted)
    {
        var json = JsonSerializer.Serialize(accepted.Value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("correlationIds")
            .EnumerateArray().Select(e => e.GetGuid()).ToList();
    }

    private static Guid ReadOrderId(AcceptedResult accepted)
    {
        var json = JsonSerializer.Serialize(accepted.Value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("orderId").GetGuid();
    }

    // Shape carrinho (novo) com 1 item — equivalente semântico do legado.
    private static readonly object SingleCartBody = new
    {
        userId = 7,
        items = new[] { new { matchId = 1, category = "VIP", quantity = 2 } },
    };

    // Shape LEGADO (single, sem items) — o smoke test do workflow depende disso.
    private static readonly object LegacySingleBody = new { matchId = 1, category = "VIP", userId = 7, quantity = 2 };

    [Fact]
    public async Task Reads_XEntraOid_Header_Into_Message()
    {
        const string oid = "33333333-4444-5555-6666-777777777777";
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(SingleCartBody, entraOidHeader: oid));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);
        Assert.Single(messages);
        Assert.Equal(Guid.Parse(oid), messages[0].EntraOid);
    }

    [Fact]
    public async Task NoHeader_Leaves_EntraOid_Null_Regression()
    {
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(SingleCartBody));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);
        Assert.Single(messages);
        Assert.Null(messages[0].EntraOid);
    }

    [Fact]
    public async Task InvalidGuid_Header_Is_Ignored_EntraOid_Null()
    {
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(SingleCartBody, entraOidHeader: "not-a-guid"));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);
        Assert.Single(messages);
        Assert.Null(messages[0].EntraOid);
    }

    [Fact]
    public async Task MultiItem_Cart_FansOut_To_N_Messages_DistinctCorrelationIds_SameOrderId()
    {
        var body = new
        {
            userId = 7,
            items = new[]
            {
                new { matchId = 1, category = "VIP", quantity = 2 },
                new { matchId = 5, category = "Cat1", quantity = 1 },
            },
        };
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(body));

        var accepted = Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);

        // 2 itens → 2 mensagens.
        Assert.Equal(2, messages.Count);

        // 2 correlationIds distintos.
        var correlationIds = ReadCorrelationIds(accepted);
        Assert.Equal(2, correlationIds.Count);
        Assert.Equal(2, correlationIds.Distinct().Count());
        Assert.Equal(2, messages.Select(m => m.CorrelationId).Distinct().Count());

        // Mesmo orderId em todas.
        var orderId = ReadOrderId(accepted);
        Assert.All(messages, m => Assert.Equal(orderId, m.OrderId));
        Assert.Single(messages.Select(m => m.OrderId).Distinct());

        // Conteúdo das linhas preservado.
        Assert.Contains(messages, m => m.MatchId == 1 && m.Category == "VIP" && m.Quantity == 2);
        Assert.Contains(messages, m => m.MatchId == 5 && m.Category == "Cat1" && m.Quantity == 1);

        // Multi-item → correlationId singular ausente.
        Assert.Null(ReadSingularCorrelationId(accepted));
    }

    [Fact]
    public async Task MultiItem_Reads_XEntraOid_Once_And_Propagates_To_All_Messages()
    {
        const string oid = "11111111-2222-3333-4444-555555555555";
        var body = new
        {
            userId = 7,
            items = new[]
            {
                new { matchId = 1, category = "VIP", quantity = 2 },
                new { matchId = 5, category = "Cat1", quantity = 1 },
                new { matchId = 9, category = "Cat2", quantity = 3 },
            },
        };
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(body, entraOidHeader: oid));

        Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);
        Assert.Equal(3, messages.Count);
        Assert.All(messages, m => Assert.Equal(Guid.Parse(oid), m.EntraOid));
    }

    [Fact]
    public async Task LegacySingle_Shape_Returns202_With_Singular_CorrelationId_Regression()
    {
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(LegacySingleBody));

        var accepted = Assert.IsType<AcceptedResult>(output.HttpResponse);
        var messages = DeserializeAll(output.Messages);
        Assert.Single(messages);
        Assert.Equal(1, messages[0].MatchId);
        Assert.Equal("VIP", messages[0].Category);
        Assert.Equal(7, messages[0].UserId);
        Assert.Equal(2, messages[0].Quantity);

        // correlationId singular DEVE estar presente e == correlationIds[0] (smoke test).
        var correlationIds = ReadCorrelationIds(accepted);
        var singular = ReadSingularCorrelationId(accepted);
        Assert.Single(correlationIds);
        Assert.NotNull(singular);
        Assert.Equal(correlationIds[0], singular!.Value);
    }

    [Fact]
    public async Task InvalidItem_In_Cart_Returns400_And_Publishes_Nothing()
    {
        // quantity = 11 (fora de 1-10) na segunda linha → all-or-nothing: 400, nada publicado.
        var body = new
        {
            userId = 7,
            items = new[]
            {
                new { matchId = 1, category = "VIP", quantity = 2 },
                new { matchId = 5, category = "Cat1", quantity = 11 },
            },
        };
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(body));

        Assert.IsType<BadRequestObjectResult>(output.HttpResponse);
        Assert.True(output.Messages is null || output.Messages.Length == 0);
    }

    [Fact]
    public async Task InvalidCategory_In_Cart_Returns400_And_Publishes_Nothing()
    {
        var body = new
        {
            userId = 7,
            items = new[]
            {
                new { matchId = 1, category = "VIP", quantity = 2 },
                new { matchId = 5, category = "Bogus", quantity = 1 },
            },
        };
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(body));

        Assert.IsType<BadRequestObjectResult>(output.HttpResponse);
        Assert.True(output.Messages is null || output.Messages.Length == 0);
    }

    [Fact]
    public async Task Empty_Items_NoLegacyFields_Returns400()
    {
        var body = new { userId = 7, items = Array.Empty<object>() };
        var sut = new PurchaseEntryFunction(NullLogger<PurchaseEntryFunction>.Instance);

        var output = await sut.RunAsync(BuildRequest(body));

        Assert.IsType<BadRequestObjectResult>(output.HttpResponse);
        Assert.True(output.Messages is null || output.Messages.Length == 0);
    }
}
