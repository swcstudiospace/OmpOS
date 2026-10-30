using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.Subagents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Sessions;

/// <summary>
/// <see cref="AcpTaskToolActivitySource"/> replays recorded tool-call events into the same updates
/// <c>RecordedTaskFrames</c> applies to the panel. Other event kinds are ignored.
/// </summary>
public sealed class AcpTaskToolActivitySourceTests
{
    /// <summary>Named payloads produce the named-frame script: one update per recorded event, with the agent name and last line.</summary>
    [Fact]
    public void NamedEventsMatchTheNamedFrameScript()
    {
        Replay(RecordedTaskEvents.WithNames).ShouldBe(
        [
            new SubagentActivityUpdateEventArgs("call-1", "researcher", SubagentStatus.Running, null),
            new SubagentActivityUpdateEventArgs("call-1", "researcher", SubagentStatus.Running, "Searching the repo."),
            new SubagentActivityUpdateEventArgs("call-2", "writer", SubagentStatus.Running, "Drafting the note."),
        ]);
    }

    /// <summary>Payloads without a name produce the unnamed-frame script.</summary>
    [Fact]
    public void UnnamedEventsMatchTheUnnamedFrameScript()
    {
        Replay(RecordedTaskEvents.WithoutNames).ShouldBe(
        [
            new SubagentActivityUpdateEventArgs("call-a", null, SubagentStatus.Running, "first line"),
            new SubagentActivityUpdateEventArgs("call-b", null, SubagentStatus.Running, "second line"),
        ]);
    }

    /// <summary>Status payloads produce the status-transition script.</summary>
    [Fact]
    public void StatusEventsMatchTheStatusFrameScript()
    {
        Replay(RecordedTaskEvents.StatusTransitions).ShouldBe(
        [
            new SubagentActivityUpdateEventArgs("call-run", "runner", SubagentStatus.Running, null),
            new SubagentActivityUpdateEventArgs("call-done", "finisher", SubagentStatus.Running, null),
            new SubagentActivityUpdateEventArgs("call-fail", "breaker", SubagentStatus.Running, null),
            new SubagentActivityUpdateEventArgs("call-stop", "halting", SubagentStatus.Running, null),
            new SubagentActivityUpdateEventArgs("call-done", null, SubagentStatus.Done, "finished the note"),
            new SubagentActivityUpdateEventArgs("call-fail", null, SubagentStatus.Failed, "the tool failed"),
            new SubagentActivityUpdateEventArgs("call-stop", null, SubagentStatus.Cancelled, "the call was cancelled"),
        ]);
    }

    /// <summary>A read tool call, its update and a text delta produce no row update.</summary>
    [Fact]
    public void OtherEventsAreIgnored()
    {
        Replay(RecordedTaskEvents.Ignored).ShouldBeEmpty();
    }

    private static List<SubagentActivityUpdateEventArgs> Replay(IReadOnlyList<Autonogrammer.Harness.Abstractions.HarnessEvent> events)
    {
        var source = new AcpTaskToolActivitySource();
        var seen = new List<SubagentActivityUpdateEventArgs>();
        source.Updated += (_, update) => seen.Add(update);
        foreach (var evt in events)
        {
            source.Observe(evt);
        }

        return seen;
    }
}
