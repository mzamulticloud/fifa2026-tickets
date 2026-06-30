using System.Net;
using System.Text.Json;
using Fifa2026.V2.Functions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fifa2026.V2.Functions.Tests;

/// <summary>
/// Story 2.4 AC-6/AC-7 — testes da implementação real do notifier:
///  - no-op silencioso quando N8N_WEBHOOK_URL não está configurado;
///  - payload POSTado no CORPO com correlationId + entraOid;
///  - falha de rede/HTTP non-2xx é capturada e NUNCA propaga (sem DLQ).
/// </summary>
public sealed class N8nWebhookNotifierTests
{
    /// <summary>Handler stub que captura a request e devolve uma resposta configurável (ou lança).</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _status;
        private readonly Exception? _throw;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        public int CallCount { get; private set; }

        public StubHandler(HttpStatusCode status) => _status = status;
        public StubHandler(Exception toThrow) => _throw = toThrow;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_throw is not null)
            {
                throw _throw;
            }

            return new HttpResponseMessage(_status!.Value);
        }
    }

    private static IConfiguration Config(string? webhookUrl)
    {
        var dict = new Dictionary<string, string?>();
        if (webhookUrl is not null)
        {
            dict[N8nWebhookNotifier.WebhookUrlSetting] = webhookUrl;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static N8nWebhookPayload SamplePayload(Guid? oid = null) => new()
    {
        CorrelationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
        MatchId = 1,
        Category = "VIP",
        EntraOid = oid
    };

    [Fact]
    public async Task No_url_configured_is_noop_and_does_not_call_http()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var sut = new N8nWebhookNotifier(http, Config(webhookUrl: null), NullLogger<N8nWebhookNotifier>.Instance);

        await sut.NotifyPurchaseAsync(SamplePayload());

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Empty_url_configured_is_noop_and_does_not_call_http()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var sut = new N8nWebhookNotifier(http, Config("   "), NullLogger<N8nWebhookNotifier>.Instance);

        await sut.NotifyPurchaseAsync(SamplePayload());

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Posts_json_body_with_correlationId_and_entraOid()
    {
        var oid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new StubHandler(HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var sut = new N8nWebhookNotifier(
            http, Config("https://n8n.example/webhook/purchase"), NullLogger<N8nWebhookNotifier>.Instance);

        await sut.NotifyPurchaseAsync(SamplePayload(oid));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.NotNull(handler.LastBody);

        using var doc = JsonDocument.Parse(handler.LastBody!);
        var root = doc.RootElement;
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", root.GetProperty("correlationId").GetString());
        Assert.Equal(1, root.GetProperty("matchId").GetInt32());
        Assert.Equal("VIP", root.GetProperty("category").GetString());
        Assert.Equal(oid.ToString(), root.GetProperty("entraOid").GetString());
    }

    [Fact]
    public async Task Http_non_2xx_does_not_throw()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError);
        var http = new HttpClient(handler);
        var sut = new N8nWebhookNotifier(
            http, Config("https://n8n.example/webhook/purchase"), NullLogger<N8nWebhookNotifier>.Instance);

        var exception = await Record.ExceptionAsync(() => sut.NotifyPurchaseAsync(SamplePayload()));

        Assert.Null(exception);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Network_failure_does_not_throw()
    {
        // Falha de rede (n8n down) → fire-and-forget engole a exceção (sem DLQ).
        var handler = new StubHandler(new HttpRequestException("connection refused"));
        var http = new HttpClient(handler);
        var sut = new N8nWebhookNotifier(
            http, Config("https://n8n.example/webhook/purchase"), NullLogger<N8nWebhookNotifier>.Instance);

        var exception = await Record.ExceptionAsync(() => sut.NotifyPurchaseAsync(SamplePayload()));

        Assert.Null(exception);
    }
}
