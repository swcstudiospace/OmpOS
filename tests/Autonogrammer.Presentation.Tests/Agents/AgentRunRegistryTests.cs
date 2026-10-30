using Autonogrammer.Presentation.Agents;
using Shouldly;
using Xunit;
using Status = Autonogrammer.Presentation.Agents.AgentRunStatus;

namespace Autonogrammer.Presentation.Tests.Agents;

/// <summary>
/// The ADE-05 run lifecycle on <see cref="AgentRunRegistry"/>: three concurrent runs, the transition table, and refused transitions
/// that leave the run unchanged. The runs are driven step by step on one thread, as the registry requires.
/// </summary>
public sealed class AgentRunRegistryTests
{
    /// <summary>Three runs execute at the same time, each reaches its own status, and the statuses stay independent.</summary>
    [Fact]
    public void ThreeConcurrentRunsEachReachTheirOwnStatus()
    {
        var registry = new AgentRunRegistry();
        var build = registry.Add("build", "Build the app");
        var tests = registry.Add("tests", "Run the tests");
        var docs = registry.Add("docs", "Update the docs");

        // Round 1: all three are running and each emits its first entry.
        build.AppendTranscript("build: compiling");
        tests.AppendTranscript("tests: discovering");
        docs.AppendTranscript("docs: reading");
        registry.Runs.Select(run => run.Status).ShouldAllBe(status => status == Status.Running);

        // Round 2: docs completes while the other two keep running.
        tests.AppendTranscript("tests: 12 passed, 1 failed");
        registry.Transition("docs", Status.Completed);
        build.Status.ShouldBe(Status.Running);
        tests.Status.ShouldBe(Status.Running);
        docs.Status.ShouldBe(Status.Completed);

        // Round 3: tests fails and build is cancelled.
        registry.Transition("tests", Status.Failed);
        build.AppendTranscript("build: linking");
        registry.Transition("build", Status.Cancelled);

        registry.Runs.Select(run => run.Status).ShouldBe([Status.Cancelled, Status.Failed, Status.Completed]);
        build.Transcript.ShouldBe(["build: compiling", "build: linking"]);
        tests.Transcript.ShouldBe(["tests: discovering", "tests: 12 passed, 1 failed"]);
        docs.Transcript.ShouldBe(["docs: reading"]);
    }

    /// <summary>Each status a running run may reach is accepted, and the change is announced once.</summary>
    /// <param name="target">The status the run moves to.</param>
    [Theory]
    [InlineData(Status.Completed)]
    [InlineData(Status.Failed)]
    [InlineData(Status.Cancelled)]
    public void RunningMovesToEachTerminalStatus(Status target)
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Refactor parser");
        var announced = PropertyChangeRecorder.Record(run);

        registry.Transition("run-1", target);

        run.Status.ShouldBe(target);
        run.StatusText.ShouldBe(target.ToString());
        announced.ShouldAnnounce(nameof(AgentRun.Status));
    }

    /// <summary>Every transition outside the table is refused with the typed exception, and the run keeps its status and transcript.</summary>
    /// <param name="from">The status the run is in.</param>
    /// <param name="to">The status that is requested.</param>
    [Theory]
    [InlineData(Status.Completed, Status.Running)]
    [InlineData(Status.Completed, Status.Failed)]
    [InlineData(Status.Completed, Status.Cancelled)]
    [InlineData(Status.Failed, Status.Running)]
    [InlineData(Status.Failed, Status.Completed)]
    [InlineData(Status.Failed, Status.Cancelled)]
    [InlineData(Status.Cancelled, Status.Running)]
    [InlineData(Status.Cancelled, Status.Completed)]
    [InlineData(Status.Cancelled, Status.Failed)]
    [InlineData(Status.Running, Status.Running)]
    public void InvalidTransitionIsRefusedAndChangesNothing(Status from, Status to)
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Refactor parser");
        run.AppendTranscript("started");
        if (from != Status.Running)
        {
            registry.Transition("run-1", from);
        }

        var announced = PropertyChangeRecorder.Record(run);

        var error = Should.Throw<AgentRunTransitionException>(() => registry.Transition("run-1", to));

        error.RunId.ShouldBe("run-1");
        error.From.ShouldBe(from);
        error.To.ShouldBe(to);
        run.Status.ShouldBe(from);
        run.Transcript.ShouldBe(["started"]);
        announced.ShouldBeEmpty();
    }

    /// <summary>A status outside the enum is rejected before any run is looked up, and the run is unchanged.</summary>
    [Fact]
    public void UndefinedStatusIsRejected()
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Refactor parser");

        Should.Throw<ArgumentOutOfRangeException>(() => registry.Transition("run-1", (Status)99));

        run.Status.ShouldBe(Status.Running);
    }

    /// <summary>A transition for an identifier the registry does not hold is rejected and changes no run.</summary>
    [Fact]
    public void TransitionForAnUnknownRunIsRejected()
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Refactor parser");

        Should.Throw<ArgumentException>(() => registry.Transition("run-9", Status.Completed));

        run.Status.ShouldBe(Status.Running);
    }

    /// <summary>A new run starts running, and an identifier can be used only once.</summary>
    [Fact]
    public void NewRunStartsRunningAndIdentifiersAreUnique()
    {
        var registry = new AgentRunRegistry();
        var run = registry.Add("run-1", "Refactor parser");

        run.Status.ShouldBe(Status.Running);
        Should.Throw<ArgumentException>(() => registry.Add("run-1", "Another title"));
        registry.Runs.ShouldBe([run]);
    }
}
