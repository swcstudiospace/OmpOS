using Autonogrammer.Presentation.Agents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Agents;

/// <summary>Two concurrent runs keep their transcripts apart (ADE-05).</summary>
public sealed class AgentRunTranscriptTests
{
    /// <summary>Appending to one run leaves the other run's transcript and announcements unchanged.</summary>
    [Fact]
    public void AppendingToOneRunDoesNotChangeTheOther()
    {
        var registry = new AgentRunRegistry();
        var first = registry.Add("first", "Build the app");
        var second = registry.Add("second", "Run the tests");
        var secondAnnounced = PropertyChangeRecorder.Record(second);

        first.AppendTranscript("first: compiling");
        first.AppendTranscript("first: linking");

        first.Transcript.ShouldBe(["first: compiling", "first: linking"]);
        second.Transcript.ShouldBeEmpty();
        secondAnnounced.ShouldBeEmpty();

        second.AppendTranscript("second: discovering");

        first.Transcript.ShouldBe(["first: compiling", "first: linking"]);
        second.Transcript.ShouldBe(["second: discovering"]);
    }

    /// <summary>Appending an entry never changes a run's status.</summary>
    [Fact]
    public void AppendingDoesNotChangeStatus()
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Build the app");

        run.AppendTranscript("compiling");

        run.Status.ShouldBe(AgentRunStatus.Running);
    }

    /// <summary>A null transcript entry is rejected and the transcript is unchanged.</summary>
    [Fact]
    public void NullEntryIsRejected()
    {
        var run = new AgentRunRegistry().Add("run-1", "Build the app");

        Should.Throw<ArgumentNullException>(() => run.AppendTranscript(null!));

        run.Transcript.ShouldBeEmpty();
    }
}
