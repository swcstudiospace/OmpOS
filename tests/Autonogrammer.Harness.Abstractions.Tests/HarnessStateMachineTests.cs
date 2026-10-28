using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Abstractions.Tests;

/// <summary>The explicit transition table and the exhaustive 8×8 legality check.</summary>
public sealed class HarnessStateMachineTests
{
    private static readonly (HarnessState From, HarnessState To)[] AllowedPairs =
    [
        (HarnessState.Disconnected, HarnessState.Connecting),
        (HarnessState.Connecting, HarnessState.Handshaking),
        (HarnessState.Connecting, HarnessState.Disconnected),
        (HarnessState.Connecting, HarnessState.Faulted),
        (HarnessState.Handshaking, HarnessState.Ready),
        (HarnessState.Handshaking, HarnessState.Disconnected),
        (HarnessState.Handshaking, HarnessState.Faulted),
        (HarnessState.Ready, HarnessState.Streaming),
        (HarnessState.Ready, HarnessState.Connecting),
        (HarnessState.Ready, HarnessState.Disposed),
        (HarnessState.Ready, HarnessState.Faulted),
        (HarnessState.Streaming, HarnessState.Cancelling),
        (HarnessState.Streaming, HarnessState.Ready),
        (HarnessState.Streaming, HarnessState.Faulted),
        (HarnessState.Cancelling, HarnessState.Ready),
        (HarnessState.Cancelling, HarnessState.Faulted),
        (HarnessState.Cancelling, HarnessState.Disconnected),
        (HarnessState.Faulted, HarnessState.Connecting),
        (HarnessState.Faulted, HarnessState.Disposed),
    ];

    /// <summary>Canonical legal path from Disconnected to each state, used to position a machine.</summary>
    private static readonly Dictionary<HarnessState, HarnessState[]> PathTo = new()
    {
        [HarnessState.Disconnected] = [],
        [HarnessState.Connecting] = [HarnessState.Connecting],
        [HarnessState.Handshaking] = [HarnessState.Connecting, HarnessState.Handshaking],
        [HarnessState.Ready] = [HarnessState.Connecting, HarnessState.Handshaking, HarnessState.Ready],
        [HarnessState.Streaming] = [HarnessState.Connecting, HarnessState.Handshaking, HarnessState.Ready, HarnessState.Streaming],
        [HarnessState.Cancelling] = [HarnessState.Connecting, HarnessState.Handshaking, HarnessState.Ready, HarnessState.Streaming, HarnessState.Cancelling],
        [HarnessState.Faulted] = [HarnessState.Connecting, HarnessState.Faulted],
        [HarnessState.Disposed] = [HarnessState.Connecting, HarnessState.Handshaking, HarnessState.Ready, HarnessState.Disposed],
    };

    /// <summary>Every legal move in the table, one row each.</summary>
    public static TheoryData<HarnessState, HarnessState> AllowedMoves
    {
        get
        {
            var data = new TheoryData<HarnessState, HarnessState>();
            foreach (var (from, to) in AllowedPairs)
            {
                data.Add(from, to);
            }

            return data;
        }
    }

    /// <summary>Every one of the 64 state pairs that is not in the table.</summary>
    public static TheoryData<HarnessState, HarnessState> DisallowedMoves
    {
        get
        {
            var data = new TheoryData<HarnessState, HarnessState>();
            foreach (var from in Enum.GetValues<HarnessState>())
            {
                foreach (var to in Enum.GetValues<HarnessState>())
                {
                    if (!AllowedPairs.Contains((from, to)))
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>A new machine starts Disconnected.</summary>
    [Fact]
    public void NewMachineStartsDisconnected()
    {
        new HarnessStateMachine().State.ShouldBe(HarnessState.Disconnected);
    }

    /// <summary>The table holds exactly the 19 specified moves, and Disposed has none.</summary>
    [Fact]
    public void AllowedTableMatchesSpecification()
    {
        var table = HarnessStateMachine.AllowedTransitions;

        table.Count.ShouldBe(8);
        table.Values.Sum(targets => targets.Count).ShouldBe(AllowedPairs.Length);
        AllowedPairs.Length.ShouldBe(19);
        table[HarnessState.Disposed].ShouldBeEmpty();
        foreach (var (from, to) in AllowedPairs)
        {
            table[from].ShouldContain(to);
        }
    }

    /// <summary>Every allowed move is accepted and lands in the target state.</summary>
    [Theory]
    [MemberData(nameof(AllowedMoves))]
    public void AllowedTransitionIsAccepted(HarnessState from, HarnessState to)
    {
        var machine = MachineIn(from);

        machine.TransitionTo(to);

        machine.State.ShouldBe(to);
    }

    /// <summary>Every pair outside the table throws InvalidHarnessTransitionException and leaves the state unchanged.</summary>
    [Theory]
    [MemberData(nameof(DisallowedMoves))]
    public void DisallowedTransitionThrowsAndKeepsState(HarnessState from, HarnessState to)
    {
        var machine = MachineIn(from);

        var error = Should.Throw<InvalidHarnessTransitionException>(() => machine.TransitionTo(to));

        error.From.ShouldBe(from);
        error.To.ShouldBe(to);
        machine.State.ShouldBe(from);
    }

    private static HarnessStateMachine MachineIn(HarnessState state)
    {
        var machine = new HarnessStateMachine();
        foreach (var step in PathTo[state])
        {
            machine.TransitionTo(step);
        }

        machine.State.ShouldBe(state);
        return machine;
    }
}
