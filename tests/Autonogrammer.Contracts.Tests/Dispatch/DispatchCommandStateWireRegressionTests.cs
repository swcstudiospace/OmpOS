using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>Every hub state has exactly one wire name, and JSON uses that name.</summary>
public sealed class DispatchCommandStateWireRegressionTests
{
    private static readonly (DispatchCommandState State, string Wire)[] Names =
    [
        (DispatchCommandState.Queued, "queued"),
        (DispatchCommandState.Delivered, "delivered"),
        (DispatchCommandState.Received, "received"),
        (DispatchCommandState.AwaitingApproval, "awaiting_approval"),
        (DispatchCommandState.Accepted, "accepted"),
        (DispatchCommandState.Running, "running"),
        (DispatchCommandState.Completed, "completed"),
        (DispatchCommandState.Failed, "failed"),
        (DispatchCommandState.Cancelled, "cancelled"),
        (DispatchCommandState.Expired, "expired"),
    ];

    /// <summary>The name table covers every state, and each name parses back to its state.</summary>
    [Fact]
    public void EveryStateHasOneWireNameThatParsesBack()
    {
        Names.Length.ShouldBe(Enum.GetValues<DispatchCommandState>().Length);
        foreach (var (state, wire) in Names)
        {
            DispatchCommandStateWire.ToWire(state).ShouldBe(wire);
            DispatchCommandStateWire.TryParse(wire, out var parsed).ShouldBeTrue();
            parsed.ShouldBe(state);
        }
    }

    /// <summary>JSON writes and reads the same wire names.</summary>
    [Fact]
    public void JsonUsesTheWireNames()
    {
        foreach (var (state, wire) in Names)
        {
            var json = JsonSerializer.Serialize(state, DispatchJsonContext.Default.DispatchCommandState);
            json.ShouldBe($"\"{wire}\"");
            JsonSerializer.Deserialize(json, DispatchJsonContext.Default.DispatchCommandState).ShouldBe(state);
        }
    }

    /// <summary>A name outside the closed set is refused by JSON and by TryParse.</summary>
    [Fact]
    public void AnUnknownNameIsRefused()
    {
        DispatchCommandStateWire.TryParse("duplicate", out _).ShouldBeFalse();
        DispatchCommandStateWire.TryParse(null, out _).ShouldBeFalse();
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("\"duplicate\"", DispatchJsonContext.Default.DispatchCommandState));
    }
}
