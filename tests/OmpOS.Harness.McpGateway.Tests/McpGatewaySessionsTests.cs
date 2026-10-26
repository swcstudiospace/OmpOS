using Shouldly;
using Xunit;

namespace OmpOS.Harness.McpGateway.Tests;

/// <summary>Transport sessions are bound to exactly one registry session.</summary>
public sealed class McpGatewaySessionsTests
{
    [Fact]
    public void TransportSessionIsBoundToItsRegistrySessionOnly()
    {
        var sessions = new McpGatewaySessions();
        sessions.Open("a");
        sessions.Open("b");

        var transport = sessions.BeginTransport("a");

        sessions.IsBound(transport, "a").ShouldBeTrue();
        sessions.IsBound(transport, "b").ShouldBeFalse();
        sessions.IsBound("unknown", "a").ShouldBeFalse();
        sessions.EndTransport(transport, "b").ShouldBeFalse();
        sessions.IsBound(transport, "a").ShouldBeTrue();
        sessions.EndTransport(transport, "a").ShouldBeTrue();
        sessions.IsBound(transport, "a").ShouldBeFalse();
    }

    [Fact]
    public void ClosingARegistrySessionEndsItsTransportSessionsOnly()
    {
        var sessions = new McpGatewaySessions();
        sessions.Open("a");
        sessions.Open("b");
        var first = sessions.BeginTransport("a");
        var second = sessions.BeginTransport("a");
        var other = sessions.BeginTransport("b");

        sessions.Close("a").ShouldBeTrue();

        sessions.IsOpen("a").ShouldBeFalse();
        sessions.IsBound(first, "a").ShouldBeFalse();
        sessions.IsBound(second, "a").ShouldBeFalse();
        sessions.IsOpen("b").ShouldBeTrue();
        sessions.IsBound(other, "b").ShouldBeTrue();
        sessions.Close("a").ShouldBeFalse();
    }

    [Fact]
    public void TransportSessionIdsAreUnique()
    {
        var sessions = new McpGatewaySessions();
        sessions.Open("a");

        var ids = Enumerable.Range(0, 64).Select(_ => sessions.BeginTransport("a")).ToList();

        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
    }
}
