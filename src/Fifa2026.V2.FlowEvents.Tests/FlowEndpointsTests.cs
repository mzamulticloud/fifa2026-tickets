using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fifa2026.V2.FlowEvents.Data;
using Fifa2026.V2.FlowEvents.Hubs;
using Fifa2026.V2.FlowEvents.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fifa2026.V2.FlowEvents.Tests;

/// <summary>
/// AC-3/AC-5/AC-6 — testes ponta-a-ponta dos endpoints via WebApplicationFactory,
/// com App Insights MOCADO (FakeFlowEventRepository) e SignalR MOCADO
/// (RecordingFlowEventPublisher) — Story 2.6 Testing approach. Sem workspace App
/// Insights nem SignalR Service reais.
/// </summary>
public sealed class FlowEndpointsTests : IClassFixture<FlowEndpointsTests.FlowAppFactory>
{
    private readonly FlowAppFactory _factory;

    public FlowEndpointsTests(FlowAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_responds_healthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("flow-events", body.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Timeline_returns_six_nodes_with_gateway_yarp_as_node_zero()
    {
        var client = _factory.CreateClient();
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

        var timeline = await client.GetFromJsonAsync<List<FlowEvent>>($"/api/flow/{id}");

        Assert.NotNull(timeline);
        Assert.Equal(6, timeline!.Count);
        // AC-13 — o primeiro nó é o Gateway YARP (nó zero), NUNCA APIM.
        Assert.Equal(FlowEventType.GATEWAY_YARP_RECEIVED, timeline[0].EventType);
        Assert.Equal(0, timeline[0].NodeIndex);
        Assert.Equal(FlowEventType.SQL_INSERTED, timeline[5].EventType);
    }

    [Fact]
    public async Task Timeline_rejects_non_guid_correlation_id()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/flow/not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recent_returns_purchases_list()
    {
        var client = _factory.CreateClient();
        var purchases = await client.GetFromJsonAsync<List<RecentPurchase>>("/api/flow/recent");
        Assert.NotNull(purchases);
        Assert.NotEmpty(purchases!);
    }

    [Fact]
    public async Task Replay_pushes_each_event_to_signalr_group()
    {
        var client = _factory.CreateClient();
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        _factory.Publisher.Reset();

        var response = await client.PostAsync($"/api/flow/{id}/replay", content: null);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, body.GetProperty("pushed").GetInt32());

        // Cada um dos 6 eventos foi empurrado via SignalR, em ordem de nó.
        Assert.Equal(6, _factory.Publisher.Published.Count);
        Assert.Equal(FlowEventType.GATEWAY_YARP_RECEIVED, _factory.Publisher.Published[0].EventType);
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    public sealed class FlowAppFactory : WebApplicationFactory<Program>
    {
        public RecordingFlowEventPublisher Publisher { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFlowEventRepository>();
                services.AddSingleton<IFlowEventRepository, FakeFlowEventRepository>();

                services.RemoveAll<IFlowEventPublisher>();
                services.AddSingleton<IFlowEventPublisher>(Publisher);
            });
        }
    }

    private sealed class FakeFlowEventRepository : IFlowEventRepository
    {
        public Task<IReadOnlyList<FlowEvent>> GetTimelineAsync(string correlationId, CancellationToken cancellationToken = default)
        {
            // Os 6 hops REAIS em ordem (Gateway YARP → SQL).
            var timeline = Enum.GetValues<FlowEventType>()
                .OrderBy(t => (int)t)
                .Select(t => new FlowEvent
                {
                    CorrelationId = correlationId,
                    EventType = t,
                    Timestamp = DateTimeOffset.UtcNow,
                    Status = "ok",
                    Message = t.ToString()
                })
                .ToList();
            return Task.FromResult<IReadOnlyList<FlowEvent>>(timeline);
        }

        public Task<IReadOnlyList<RecentPurchase>> GetRecentPurchasesAsync(int top, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<RecentPurchase> list = new[]
            {
                new RecentPurchase { CorrelationId = Guid.NewGuid().ToString(), Timestamp = DateTimeOffset.UtcNow, Status = "ok" }
            };
            return Task.FromResult(list);
        }
    }

    public sealed class RecordingFlowEventPublisher : IFlowEventPublisher
    {
        public List<FlowEvent> Published { get; } = new();

        public void Reset() => Published.Clear();

        public Task PublishAsync(FlowEvent flowEvent, CancellationToken cancellationToken = default)
        {
            Published.Add(flowEvent);
            return Task.CompletedTask;
        }
    }
}
