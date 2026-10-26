using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static OmpOS.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace OmpOS.Presentation.Tests.AgentPanel;

/// <summary>
/// The Context tab model: states Loading, Ready, Partial and Unavailable, the exact copy of each, caps and plain text for notes,
/// and cancellation of a load that a newer selection supersedes (46-UI-SPEC, Context Tab).
/// </summary>
public sealed class TaskContextModelTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly List<TaskCompletionSource<TaskContext>> _gates = [];

    /// <summary>A model with no source and no selection shows the no-task copy.</summary>
    [Fact]
    public async Task WithoutASourceTheTabSaysThereIsNoTask()
    {
        using var model = new TaskContextModel(null, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Unavailable);
        model.Notice.ShouldBe(TaskContextNotice.NoTask);
        model.Heading.ShouldBe("No task for this agent.");
        model.Body.ShouldBe("This agent is not bound to a task. Bind one to see its status, phase, shared memory and lessons.");
        model.HasCard.ShouldBeFalse();
    }

    /// <summary>A load shows the loading heading with no body, then the task card.</summary>
    [Fact]
    public async Task ALoadGoesFromLoadingToReady()
    {
        using var model = new TaskContextModel(GatedSource(), _clock);

        var load = model.SelectAsync(Scope());

        model.State.ShouldBe(TaskContextState.Loading);
        model.Heading.ShouldBe("Loading task context\u2026");
        model.Body.ShouldBeNull();
        model.HasCard.ShouldBeFalse();

        _gates[0].SetResult(Context(phase: new GsdPhaseInfo { Number = "46", Name = "Agent Panel", Status = "in progress", PlansComplete = 2, PlansTotal = 5 }));
        await load.ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Ready);
        model.Notice.ShouldBe(TaskContextNotice.None);
        model.Heading.ShouldBeNull();
        model.Body.ShouldBeNull();
        model.HasCard.ShouldBeTrue();
        model.Title.ShouldBe("Ship the panel");
        model.TaskId.ShouldBe("task-7");
        model.KanbanStatus.ShouldBe("In progress");
        model.GraphId.ShouldBe("ut-mvp0bh59-2dd6ef4c");
        model.WorkflowText.ShouldBe("Phase 46 \u00B7 Agent Panel, plan 2 of 5");
        model.Phase!.Number.ShouldBe("46");
    }

    /// <summary>A context with warnings is Partial: the card and lists show, the body says some was not read, and the warnings are listed.</summary>
    [Fact]
    public async Task WarningsMakeTheContextPartial()
    {
        var source = ScriptedContextSource.Returning(Context(warnings: ["Substrate memory could not be read.", "GSD tools were not found."]));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Partial);
        model.Notice.ShouldBe(TaskContextNotice.Partial);
        model.Heading.ShouldBeNull();
        model.Body.ShouldBe("Some context could not be read.");
        model.HasCard.ShouldBeTrue();
        model.WarningsHeader.ShouldBe("Could not read");
        model.Warnings.ShouldBe(["Substrate memory could not be read.", "GSD tools were not found."]);
        model.WorkflowText.ShouldBe("Phase unknown");
    }

    /// <summary>A context that carries nothing but no warning means the agent has no task.</summary>
    [Fact]
    public async Task AnEmptyContextMeansNoTask()
    {
        var source = ScriptedContextSource.Returning(Context(taskId: null, kanban: null));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Unavailable);
        model.Notice.ShouldBe(TaskContextNotice.NoTask);
        model.HasCard.ShouldBeFalse();
    }

    /// <summary>A context that carries nothing but a warning is unavailable and says the warning.</summary>
    [Fact]
    public async Task AWarningOnlyContextIsUnavailableWithTheFirstWarning()
    {
        var source = ScriptedContextSource.Returning(Context(taskId: null, kanban: null, warnings: ["Substrate is not reachable.", "second"]));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Unavailable);
        model.Notice.ShouldBe(TaskContextNotice.Failed);
        model.Heading.ShouldBe("Task context is unavailable.");
        model.Body.ShouldBe("Substrate is not reachable.");
    }

    /// <summary>A source that throws makes the tab unavailable with the fallback body and never shows the exception text.</summary>
    [Fact]
    public async Task AThrowingSourceIsUnavailableWithoutTheExceptionText()
    {
        var source = new ScriptedContextSource((_, _) => throw new InvalidOperationException("token=sk-secret leaked"));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Unavailable);
        model.Heading.ShouldBe("Task context is unavailable.");
        model.Body.ShouldBe("Try again after the agent finishes its next turn.");
        model.Body.ShouldNotBeNull().ShouldNotContain("secret");
        model.HasCard.ShouldBeFalse();
    }

    /// <summary>A source that cancels itself, with no newer selection, is unavailable too.</summary>
    [Fact]
    public async Task ASourceThatCancelsItselfIsUnavailable()
    {
        var source = new ScriptedContextSource((_, _) => throw new OperationCanceledException());
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Unavailable);
        model.Notice.ShouldBe(TaskContextNotice.Failed);
    }

    /// <summary>
    /// Rapid selection changes cancel the earlier load, and a late answer from a source that ignores its token never replaces what the
    /// newest selection shows.
    /// </summary>
    [Fact]
    public async Task ARapidSelectionChangeCancelsTheEarlierLoadAndIgnoresItsLateAnswer()
    {
        var source = GatedSource();
        using var model = new TaskContextModel(source, _clock);

        var first = model.SelectAsync(Scope("s1"));
        var second = model.SelectAsync(Scope("s2"));
        var third = model.SelectAsync(Scope("s3"));

        source.Requests.Select(request => request.Scope.SessionId).ShouldBe(["s1", "s2", "s3"]);
        source.Requests[0].Token.IsCancellationRequested.ShouldBeTrue();
        source.Requests[1].Token.IsCancellationRequested.ShouldBeTrue();
        source.Requests[2].Token.IsCancellationRequested.ShouldBeFalse();

        _gates[0].SetResult(Context(title: "from s1"));
        await first.ConfigureAwait(true);
        model.State.ShouldBe(TaskContextState.Loading);

        _gates[2].SetResult(Context(title: "from s3"));
        await third.ConfigureAwait(true);
        model.Title.ShouldBe("from s3");

        _gates[1].SetResult(Context(title: "from s2"));
        await second.ConfigureAwait(true);
        model.Title.ShouldBe("from s3");
        model.State.ShouldBe(TaskContextState.Ready);
    }

    /// <summary>A cooperative source whose load is cancelled by a newer selection ends quietly and the tab keeps loading the newer one.</summary>
    [Fact]
    public async Task ACancelledEarlierLoadDoesNotShowAnError()
    {
        var source = GatedSource(cooperative: true);
        using var model = new TaskContextModel(source, _clock);

        var first = model.SelectAsync(Scope("s1"));
        var second = model.SelectAsync(Scope("s2"));
        await first.ConfigureAwait(true);

        model.State.ShouldBe(TaskContextState.Loading);
        model.Notice.ShouldBe(TaskContextNotice.Loading);

        _gates[1].SetResult(Context(title: "from s2"));
        await second.ConfigureAwait(true);

        model.Title.ShouldBe("from s2");
    }

    /// <summary>Clearing the selection cancels the running load and shows the no-task copy.</summary>
    [Fact]
    public async Task ClearingTheSelectionCancelsTheLoad()
    {
        var source = GatedSource();
        using var model = new TaskContextModel(source, _clock);
        var load = model.SelectAsync(Scope());

        var cleared = model.SelectAsync(null);

        source.Requests[0].Token.IsCancellationRequested.ShouldBeTrue();
        model.Notice.ShouldBe(TaskContextNotice.NoTask);
        _gates[0].SetResult(Context(title: "late"));
        await load.ConfigureAwait(true);
        await cleared.ConfigureAwait(true);
        model.HasCard.ShouldBeFalse();
    }

    /// <summary>Cancel stops the running load; its late answer is dropped and the tab is unavailable.</summary>
    [Fact]
    public async Task CancelStopsTheRunningLoad()
    {
        var source = GatedSource();
        using var model = new TaskContextModel(source, _clock);
        var load = model.SelectAsync(Scope());

        model.Cancel();

        source.Requests[0].Token.IsCancellationRequested.ShouldBeTrue();
        model.State.ShouldBe(TaskContextState.Unavailable);
        _gates[0].SetResult(Context(title: "late"));
        await load.ConfigureAwait(true);
        model.State.ShouldBe(TaskContextState.Unavailable);
        model.HasCard.ShouldBeFalse();
    }

    /// <summary>Cancel with nothing running changes nothing, and disposing cancels a running load.</summary>
    [Fact]
    public async Task CancelWithNothingRunningIsHarmlessAndDisposeCancels()
    {
        var source = GatedSource();
        var model = new TaskContextModel(source, _clock);
        model.Cancel();
        model.State.ShouldBe(TaskContextState.Unavailable);
        var load = model.SelectAsync(Scope());

        model.Dispose();

        source.Requests[0].Token.IsCancellationRequested.ShouldBeTrue();
        _gates[0].SetResult(Context());
        await load.ConfigureAwait(true);
        model.HasCard.ShouldBeFalse();
        await model.SelectAsync(Scope()).ConfigureAwait(true);
        source.Requests.Count.ShouldBe(1);
    }

    /// <summary>Refresh reloads the current selection, through the command too.</summary>
    [Fact]
    public async Task RefreshReloadsTheCurrentSelection()
    {
        var count = 0;
        var source = new ScriptedContextSource((_, _) => Task.FromResult(Context(title: $"load {++count}")));
        using var model = new TaskContextModel(source, _clock);
        await model.SelectAsync(Scope("s1")).ConfigureAwait(true);
        model.Title.ShouldBe("load 1");

        await model.RefreshAsync().ConfigureAwait(true);
        model.Title.ShouldBe("load 2");

        await model.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
        model.Title.ShouldBe("load 3");
        source.Requests.ShouldAllBe(request => request.Scope.SessionId == "s1");
    }

    /// <summary>Lists are capped: 20 notes are kept, 8 are shown, the rest is counted, and each note is capped and plain.</summary>
    [Fact]
    public async Task NotesAreCappedAndPlain()
    {
        var long600 = new string('a', 600);
        var memory = Enumerable.Range(0, 30).Select(index => Note($"m{index}", index == 0 ? "\u202E<b>bold</b> [link](http://x)\u0007 " + long600 : long600)).ToList();
        var source = ScriptedContextSource.Returning(Context(memory: memory));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        var list = model.SharedMemory;
        list.Header.ShouldBe("Shared memory (30)");
        list.Total.ShouldBe(30);
        list.Notes.Count.ShouldBe(TaskContextModel.MaxNotes);
        list.Visible.Count.ShouldBe(TaskContextModel.NotesShown);
        list.MoreText.ShouldBe("+22 more not shown");
        list.Notes[1].Text.Length.ShouldBe(TaskContextModel.MaxNoteChars + 1);
        list.Notes[1].Text.ShouldEndWith("\u2026");
        list.Notes[1].DisplayText.Length.ShouldBe(TaskContextModel.NoteDisplayChars + 1);
        var first = list.Notes[0];
        first.Text.ShouldStartWith("<b>bold</b> [link](http://x) aaa");
        first.Text.ShouldNotContain("\u202E");
        first.Text.ShouldNotContain("\u0007");
        model.Lessons.IsEmpty.ShouldBeTrue();
        model.Lessons.EmptyText.ShouldBe("No lessons recorded for this task.");
        model.Lessons.Header.ShouldBe("Lessons (0)");
        model.Lessons.MoreText.ShouldBeNull();
    }

    /// <summary>The meta line is the origin and the relative time, and just the origin when no time is known.</summary>
    [Fact]
    public async Task TheMetaLineIsOriginAndRelativeTime()
    {
        var recorded = _clock.GetUtcNow().AddMinutes(-5);
        var lessons = new[] { Note("l1", "Run the gate twice.", origin: "project", recordedAt: recorded), Note("l2", "No time.", origin: "global") };
        var source = ScriptedContextSource.Returning(Context(lessons: lessons));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.Lessons.Notes.Select(note => note.MetaText).ShouldBe(["project \u00B7 5 min ago", "global"]);
        model.Lessons.MoreText.ShouldBeNull();
        model.Lessons.Header.ShouldBe("Lessons (2)");
    }

    /// <summary>An empty memory list carries its own empty copy.</summary>
    [Fact]
    public async Task AnEmptyMemoryListCarriesItsEmptyCopy()
    {
        var source = ScriptedContextSource.Returning(Context());
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.SharedMemory.IsEmpty.ShouldBeTrue();
        model.SharedMemory.EmptyText.ShouldBe("No shared memory for this agent.");
    }

    /// <summary>Warnings are plain and capped in number.</summary>
    [Fact]
    public async Task WarningsArePlainAndCapped()
    {
        var warnings = Enumerable.Range(0, 30).Select(index => index == 0 ? "\u202Ebad\u0007 warning" : $"warning {index}").ToList();
        var source = ScriptedContextSource.Returning(Context(warnings: warnings));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.Warnings.Count.ShouldBe(TaskContextModel.MaxWarnings);
        model.Warnings[0].ShouldBe("bad warning");
    }

    /// <summary>A blank task title reads Untitled task.</summary>
    [Fact]
    public async Task ABlankTitleReadsUntitledTask()
    {
        var source = ScriptedContextSource.Returning(Context(title: " "));
        using var model = new TaskContextModel(source, _clock);

        await model.SelectAsync(Scope()).ConfigureAwait(true);

        model.Title.ShouldBe("Untitled task");
    }

    /// <summary>While Substrate is unreachable a loaded context carries the limited notice; the card stays, and the source is not asked again.</summary>
    [Fact]
    public async Task AnUnreachableSubstrateLimitsTheContextWithoutReloading()
    {
        var source = ScriptedContextSource.Returning(Context());
        using var model = new TaskContextModel(source, _clock);
        await model.SelectAsync(Scope()).ConfigureAwait(true);
        var announced = PropertyChangeRecorder.Record(model);

        model.SubstrateAvailable = false;

        model.Notice.ShouldBe(TaskContextNotice.SubstrateLimited);
        model.Heading.ShouldBe("Task context is limited.");
        model.Body.ShouldBe("Shared memory and lessons need Substrate, which is not reachable. The workflow phase is still shown when GSD tools are present.");
        model.HasCard.ShouldBeTrue();
        model.State.ShouldBe(TaskContextState.Ready);
        source.Requests.Count.ShouldBe(1);
        announced.ShouldAnnounce(nameof(TaskContextModel.Notice));

        model.SubstrateAvailable = true;

        model.Notice.ShouldBe(TaskContextNotice.None);
        model.Heading.ShouldBeNull();
    }

    private ScriptedContextSource GatedSource(bool cooperative = false) => new((_, token) =>
    {
        var gate = new TaskCompletionSource<TaskContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates.Add(gate);
        if (cooperative)
        {
            token.Register(() => gate.TrySetCanceled(token));
        }

        return gate.Task;
    });
}
