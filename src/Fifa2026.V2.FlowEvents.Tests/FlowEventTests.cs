using Fifa2026.V2.FlowEvents.Hubs;
using Fifa2026.V2.FlowEvents.Models;
using Xunit;

namespace Fifa2026.V2.FlowEvents.Tests;

/// <summary>AC-4/AC-6 — DTO FlowEvent e nomes de grupo SignalR.</summary>
public sealed class FlowEventTests
{
    [Fact]
    public void NodeIndex_tracks_event_type_ordinal()
    {
        var ev = new FlowEvent { EventType = FlowEventType.FUNCTION_CONSUMER_DONE };
        Assert.Equal(3, ev.NodeIndex);
    }

    [Fact]
    public void Group_name_is_scoped_per_correlation_id()
    {
        var id = "11111111-2222-3333-4444-555555555555";
        Assert.Equal($"correlation-{id}", FlowHub.GroupName(id));
    }
}
