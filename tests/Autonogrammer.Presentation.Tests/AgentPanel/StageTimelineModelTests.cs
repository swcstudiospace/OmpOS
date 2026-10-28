using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// Folding of stage events into ordered source blocks: replacement by key, ordering, children, caps, tolerance of unknown values,
/// effect lines, retry gating, pills and the run summary (46-UI-SPEC, Pipeline Strip and Pipeline Tab).
/// </summary>
public sealed class StageTimelineModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A timeline with no events shows every block as waiting and every pill as waiting.</summary>
    [Fact]
    public void AnEmptyTimelineShowsWaitingBlocksAndPills()
    {
        var model = NewModel();

        model.Blocks.Select(block => block.Source).ShouldBe([StageSource.Omp, StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd]);
        model.Blocks.ShouldAllBe(block => block.EmptyText == "Waiting for events." && block.Rows.Count == 0 && block.RollUpStatus == null);
        model.Pills.Select(pill => pill.Text).ShouldBe(["Ultrathink waiting", "Swarm waiting", "Substrate waiting", "GSD waiting"]);
        model.PipelineAccessibleName.ShouldBe("Pipeline: Ultrathink waiting, Swarm waiting, Substrate waiting, GSD waiting. Open details.");
        model.HasEvents.ShouldBeFalse();
    }

    /// <summary>A later event for the same key replaces the earlier one and the row keeps one slot.</summary>
    [Fact]
    public void ALaterEventReplacesTheEarlierOneForTheSameKey()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Running, T0)).ShouldBeTrue();

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, T0.AddSeconds(5), detail: "8 nodes, 4 waves")).ShouldBeTrue();

        var row = Rows(model, StageSource.Ultrathink).Single();
        row.Status.ShouldBe(StageStatus.Succeeded);
        row.StatusText.ShouldBe("Done");
        row.IconKey.ShouldBe(StatusIcon.Check);
        row.Detail.ShouldBe("8 nodes, 4 waves");
        row.AccessibleName.ShouldBe("plan, Done, 8 nodes, 4 waves");
    }

    /// <summary>The same stage id under two sources is two rows: the key includes the source.</summary>
    [Fact]
    public void TheSameStageIdUnderTwoSourcesIsTwoRows()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Swarm, "main", StageStatus.Running, T0));
        model.Apply(Stage(StageSource.Substrate, "main", StageStatus.Succeeded, T0));

        Rows(model, StageSource.Swarm).Single().Status.ShouldBe(StageStatus.Running);
        Rows(model, StageSource.Substrate).Single().Status.ShouldBe(StageStatus.Succeeded);
    }

    /// <summary>Rows keep the order of their first observation: an update never moves a row.</summary>
    [Fact]
    public void AnUpdateDoesNotMoveARow()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Running, T0));
        model.Apply(Stage(StageSource.Substrate, "leases", StageStatus.Running, T0.AddSeconds(1)));

        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, T0.AddSeconds(9)));

        Rows(model, StageSource.Substrate).Select(row => row.StageId).ShouldBe(["brief", "leases"]);
    }

    /// <summary>An observation older than the one the row shows is ignored, so a late file read cannot undo a newer state.</summary>
    [Fact]
    public void AnOlderObservationDoesNotReplaceANewerOne()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "run", StageStatus.Succeeded, T0.AddSeconds(10)));

        var changed = model.Apply(Stage(StageSource.Swarm, "run", StageStatus.Running, T0.AddSeconds(2)));

        changed.ShouldBeFalse();
        Rows(model, StageSource.Swarm).Single().Status.ShouldBe(StageStatus.Succeeded);
    }

    /// <summary>An event identical to the shown one changes nothing and raises nothing.</summary>
    [Fact]
    public void AnIdenticalEventChangesNothing()
    {
        var model = NewModel();
        var stageEvent = Stage(StageSource.Gsd, "phase", StageStatus.Running, T0);
        model.Apply(stageEvent);
        var announced = PropertyChangeRecorder.Record(model);

        var changed = model.Apply(stageEvent);

        changed.ShouldBeFalse();
        announced.ShouldBeEmpty();
    }

    /// <summary>Events of another session never reach this timeline.</summary>
    [Fact]
    public void EventsOfAnotherSessionAreIgnored()
    {
        var model = NewModel();

        var changed = model.Apply(Stage(StageSource.Swarm, "run", StageStatus.Failed, T0, sessionId: "session-2"));

        changed.ShouldBeFalse();
        model.HasEvents.ShouldBeFalse();
        model.DegradedStageCount.ShouldBe(0);
    }

    /// <summary>Applying an event announces the changed projections.</summary>
    [Fact]
    public void ApplyingAnEventAnnouncesBlocksAndPills()
    {
        var model = NewModel();
        var announced = PropertyChangeRecorder.Record(model);

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Running, T0));

        announced.ShouldAnnounce(nameof(StageTimelineModel.Blocks));
        announced.ShouldAnnounce(nameof(StageTimelineModel.Pills));
        announced.ShouldAnnounce(nameof(StageTimelineModel.PipelineAccessibleName));
        announced.ShouldAnnounce(nameof(StageTimelineModel.HasEvents));
    }

    /// <summary>Children follow their parent indented, whichever event arrived first.</summary>
    [Fact]
    public void ChildrenFollowTheirParentWhicheverArrivedFirst()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "a05-backend", StageStatus.Running, T0, parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, T0.AddSeconds(1)));
        model.Apply(Stage(StageSource.Swarm, "a06-frontend", StageStatus.Pending, T0.AddSeconds(2), parent: "subagents"));
        model.Apply(Stage(StageSource.Swarm, "extra", StageStatus.Running, T0.AddSeconds(3)));

        var rows = Rows(model, StageSource.Swarm);

        rows.Select(row => (row.StageId, row.Depth)).ShouldBe([("subagents", 0), ("a05-backend", 1), ("a06-frontend", 1), ("extra", 0)]);
    }

    /// <summary>A child whose parent never arrived is shown as a top-level row, not dropped.</summary>
    [Fact]
    public void AChildWithAnUnknownParentIsShownTopLevel()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Swarm, "orphan", StageStatus.Running, T0, parent: "missing"));

        var row = Rows(model, StageSource.Swarm).Single();
        row.Depth.ShouldBe(0);
        row.ParentStageId.ShouldBe("missing");
    }

    /// <summary>Stages that name each other as parents show as top-level rows and never loop.</summary>
    [Fact]
    public void ParentCyclesDoNotLoopOrDropRows()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "a", StageStatus.Running, T0, parent: "b"));
        model.Apply(Stage(StageSource.Swarm, "b", StageStatus.Running, T0.AddSeconds(1), parent: "a"));
        model.Apply(Stage(StageSource.Swarm, "self", StageStatus.Running, T0.AddSeconds(2), parent: "self"));

        var rows = Rows(model, StageSource.Swarm);

        rows.Select(row => (row.StageId, row.Depth)).ShouldBe([("self", 0), ("a", 0), ("b", 1)]);
    }

    /// <summary>A chain deeper than the cap keeps indenting no further than the cap.</summary>
    [Fact]
    public void IndentationIsCapped()
    {
        var model = NewModel();
        for (var level = 0; level < 8; level++)
        {
            model.Apply(Stage(StageSource.Swarm, $"level-{level}", StageStatus.Running, T0.AddSeconds(level), parent: level == 0 ? null : $"level-{level - 1}"));
        }

        Rows(model, StageSource.Swarm).Max(row => row.Depth).ShouldBe(StageTimelineModel.MaxDepth);
    }

    /// <summary>Blocks follow pipeline order whatever order the events arrived in.</summary>
    [Fact]
    public void BlocksFollowPipelineOrderWhateverTheArrivalOrder()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Gsd, "phase", StageStatus.Succeeded, T0));
        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, T0.AddSeconds(1)));
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, T0.AddSeconds(2)));
        model.Apply(Stage(StageSource.Omp, "engine", StageStatus.Running, T0.AddSeconds(3)));
        model.Apply(Stage(StageSource.Swarm, "run", StageStatus.Running, T0.AddSeconds(4)));

        model.Blocks.Where(block => block.Rows.Count > 0).Select(block => block.Name).ShouldBe(["Engine", "Ultrathink", "Swarm", "Substrate", "GSD"]);
        model.Blocks.Select(block => block.Source).ShouldBe(PipelineOrder);
    }

    /// <summary>An unknown source becomes a generic Other row; nothing throws.</summary>
    [Fact]
    public void AnUnknownSourceBecomesAGenericRow()
    {
        var model = NewModel();
        var unknownSource = (StageSource)42;

        model.Apply(Stage(unknownSource, "mystery", StageStatus.Failed, T0, title: "Mystery stage")).ShouldBeTrue();

        var other = model.Blocks[^1];
        other.Name.ShouldBe("Other");
        other.Source.ShouldBe(StageTimelineModel.OtherSource);
        var row = other.Rows.Single();
        row.Source.ShouldBe(unknownSource);
        row.Title.ShouldBe("Mystery stage");
        row.StatusText.ShouldBe("Failed");
        row.EffectText.ShouldBeNull();
        row.CanRetry.ShouldBeFalse();
        model.StatusAnnouncement.ShouldBeNull();
    }

    /// <summary>An unknown status reads Unknown with the info glyph, is never announced and never retried.</summary>
    [Fact]
    public void AnUnknownStatusBecomesAnUnknownRow()
    {
        var model = NewModel(recheck: NoRecheckAsync);

        model.Apply(Stage(StageSource.Swarm, "run", (StageStatus)99, T0)).ShouldBeTrue();

        var block = model.Blocks.Single(candidate => candidate.Source == StageSource.Swarm);
        var row = block.Rows.Single();
        row.IsStatusKnown.ShouldBeFalse();
        row.StatusText.ShouldBe("Unknown");
        row.IconKey.ShouldBe(StatusIcon.Info);
        row.EffectText.ShouldBeNull();
        row.CanRetry.ShouldBeFalse();
        block.RollUpStatus.ShouldBeNull();
        block.RollUpText.ShouldBe("Unknown");
        model.StatusAnnouncement.ShouldBeNull();
    }

    /// <summary>At most 200 keys are kept; a new key past the cap is counted and dropped while shown keys still update.</summary>
    [Fact]
    public void RowsAreCappedAt200AndOverflowIsCounted()
    {
        var model = NewModel();
        for (var index = 0; index < 205; index++)
        {
            model.Apply(Stage(StageSource.Omp, $"stage-{index}", StageStatus.Running, T0.AddSeconds(index)));
        }

        Rows(model, StageSource.Omp).Count.ShouldBe(StageTimelineModel.MaxRows);
        model.OverflowCount.ShouldBe(5);

        var updated = model.Apply(Stage(StageSource.Omp, "stage-3", StageStatus.Succeeded, T0.AddHours(1)));

        updated.ShouldBeTrue();
        Rows(model, StageSource.Omp).Single(row => string.Equals(row.StageId, "stage-3", StringComparison.Ordinal)).Status.ShouldBe(StageStatus.Succeeded);
        Rows(model, StageSource.Omp).Count.ShouldBe(StageTimelineModel.MaxRows);
    }

    /// <summary>Titles and details are plain text: controls and bidirectional overrides are removed and the text is capped.</summary>
    [Fact]
    public void TitleAndDetailArePlainAndCapped()
    {
        var model = NewModel();
        var detail = "\u202Eevil\u0007 " + new string('d', 400);
        var title = new string('t', 300);

        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Degraded, T0, title: title, detail: detail));

        var row = Rows(model, StageSource.Substrate).Single();
        var shown = row.Detail.ShouldNotBeNull();
        shown.ShouldNotContain("\u202E");
        shown.ShouldNotContain("\u0007");
        shown.ShouldStartWith("evil ddd");
        shown.Length.ShouldBe(StageTimelineModel.MaxDetailChars + 1);
        shown.ShouldEndWith("\u2026");
        row.Title.Length.ShouldBe(StageTimelineModel.MaxTitleChars + 1);
        row.Title.ShouldEndWith("\u2026");
    }

    /// <summary>A newline in a detail becomes a space, so a detail is one line of plain text.</summary>
    [Fact]
    public void WhitespaceControlsInADetailBecomeSpaces()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Gsd, "phase", StageStatus.Failed, T0, detail: "line one\nline two\ttabbed"));

        Rows(model, StageSource.Gsd).Single().Detail.ShouldBe("line one line two tabbed");
    }

    /// <summary>A blank title falls back to the stage id, and then to a fixed word.</summary>
    [Fact]
    public void ABlankTitleFallsBackToTheStageId()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Gsd, "phase-46", StageStatus.Running, T0, title: "  "));

        Rows(model, StageSource.Gsd).Single().Title.ShouldBe("phase-46");
    }

    /// <summary>Effect lines come from the source and status tables of the UI spec.</summary>
    [Theory]
    [InlineData(StageSource.Omp, StageStatus.Degraded, "The engine is running with reduced function.")]
    [InlineData(StageSource.Omp, StageStatus.Failed, "The engine failed. See the error above the transcript.")]
    [InlineData(StageSource.Omp, StageStatus.Unavailable, "The omp engine is not available.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Skipped, "Planning is off. Prompts run as written.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Degraded, "The planner fell back to a plain spec. No graph was created.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Failed, "Planning failed. The prompt ran as written.")]
    [InlineData(StageSource.Ultrathink, StageStatus.Unavailable, "Ultrathink is not available. Prompts run as written.")]
    [InlineData(StageSource.Swarm, StageStatus.Skipped, "Swarm is off. This agent runs alone.")]
    [InlineData(StageSource.Swarm, StageStatus.Unavailable, "Swarm is not available. This agent runs alone.")]
    [InlineData(StageSource.Substrate, StageStatus.Unavailable, "Substrate is not reachable. Steering is not recorded and shared memory is off.")]
    [InlineData(StageSource.Substrate, StageStatus.Failed, "Substrate failed. Steering is not recorded.")]
    [InlineData(StageSource.Gsd, StageStatus.Unavailable, "GSD tools were not found. The workflow phase is unknown.")]
    [InlineData(StageSource.Gsd, StageStatus.Degraded, "GSD data is partial.")]
    public void EffectLinesFollowTheSourceAndStatus(StageSource source, StageStatus status, string expected)
    {
        var model = NewModel();

        model.Apply(Stage(source, "stage", status, T0));

        model.Blocks.Single(block => block.Source == source).Rows.Single().EffectText.ShouldBe(expected);
    }

    /// <summary>Running and Done rows carry no effect line.</summary>
    [Theory]
    [InlineData(StageStatus.Pending)]
    [InlineData(StageStatus.Running)]
    [InlineData(StageStatus.Succeeded)]
    public void HealthyRowsCarryNoEffectLine(StageStatus status)
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Swarm, "stage", status, T0));

        Rows(model, StageSource.Swarm).Single().EffectText.ShouldBeNull();
    }

    /// <summary>The detail of an uninitialised submodule is shown verbatim, so the exact fix text reaches the operator.</summary>
    [Theory]
    [InlineData(StageSource.Ultrathink, "Ultrathink is not set up. plugins/claude-ultrathink is empty. Run: git submodule update --init --recursive plugins/claude-ultrathink")]
    [InlineData(StageSource.Swarm, "Swarm is not set up. plugins/agent-swarm is empty. Run: git submodule update --init --recursive plugins/agent-swarm")]
    [InlineData(StageSource.Substrate, "Substrate is not set up. plugins/agent-substrate is empty. Run: git submodule update --init --recursive plugins/agent-substrate")]
    public void AnUninitialisedSubmoduleShowsItsFixTextVerbatim(StageSource source, string fixText)
    {
        var model = NewModel(recheck: NoRecheckAsync);

        model.Apply(Stage(source, "availability", StageStatus.Unavailable, T0, detail: fixText));

        var row = model.Blocks.Single(block => block.Source == source).Rows.Single();
        row.Detail.ShouldBe(fixText);
        row.CanRetry.ShouldBeTrue();
    }

    /// <summary>Retry is offered only on failed, degraded and unavailable top-level rows of a plugin source, and only with a handler.</summary>
    [Theory]
    [InlineData(StageStatus.Pending, false)]
    [InlineData(StageStatus.Running, false)]
    [InlineData(StageStatus.Succeeded, false)]
    [InlineData(StageStatus.Skipped, false)]
    [InlineData(StageStatus.Degraded, true)]
    [InlineData(StageStatus.Failed, true)]
    [InlineData(StageStatus.Unavailable, true)]
    public void RetryIsOfferedOnlyOnRecheckableStatuses(StageStatus status, bool expected)
    {
        var withHandler = NewModel(recheck: NoRecheckAsync);
        var withoutHandler = NewModel();

        withHandler.Apply(Stage(StageSource.Substrate, "brief", status, T0));
        withoutHandler.Apply(Stage(StageSource.Substrate, "brief", status, T0));

        Rows(withHandler, StageSource.Substrate).Single().CanRetry.ShouldBe(expected);
        Rows(withoutHandler, StageSource.Substrate).Single().CanRetry.ShouldBeFalse();
    }

    /// <summary>The engine source and child rows are never re-checkable, and the retry button is named after the stage.</summary>
    [Fact]
    public void TheEngineAndChildRowsAreNotRecheckable()
    {
        var model = NewModel(recheck: NoRecheckAsync);
        model.Apply(Stage(StageSource.Omp, "engine", StageStatus.Failed, T0));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Failed, T0, title: "Subagents"));
        model.Apply(Stage(StageSource.Swarm, "a05-backend", StageStatus.Failed, T0.AddSeconds(1), parent: "subagents"));

        Rows(model, StageSource.Omp).Single().CanRetry.ShouldBeFalse();
        var swarm = Rows(model, StageSource.Swarm);
        swarm[0].CanRetry.ShouldBeTrue();
        swarm[0].RetryName.ShouldBe("Retry Subagents stage");
        swarm[1].CanRetry.ShouldBeFalse();
    }

    /// <summary>The retry command re-checks only the source it was made for, and is the same instance on every call.</summary>
    [Fact]
    public async Task TheRetryCommandReChecksOnlyItsSource()
    {
        var rechecked = new List<StageSource>();
        var model = NewModel(recheck: (source, _) =>
        {
            rechecked.Add(source);
            return Task.CompletedTask;
        });

        var command = (AgentPanelCommand)model.RetryStageCommand(StageSource.Swarm);
        await command.ExecuteAsync(null).ConfigureAwait(true);

        rechecked.ShouldBe([StageSource.Swarm]);
        model.RetryStageCommand(StageSource.Swarm).ShouldBeSameAs(command);
        model.RetryStageCommand(StageSource.Omp).CanExecute(null).ShouldBeFalse();
        NewModel().RetryStageCommand(StageSource.Swarm).CanExecute(null).ShouldBeFalse();
    }

    /// <summary>A fault of the re-check never reaches the caller; it is announced instead.</summary>
    [Fact]
    public async Task AFailingReCheckIsAnnouncedAndNotThrown()
    {
        var model = NewModel(recheck: (_, _) => throw new InvalidOperationException("probe exploded with a secret"));

        await model.RetryStageAsync(StageSource.Substrate, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.StatusAnnouncement.ShouldBe("Could not re-check Substrate.");
        model.StatusAnnouncement.ShouldNotBeNull().ShouldNotContain("secret");
    }

    /// <summary>The roll-up of a source is its worst top-level status.</summary>
    [Theory]
    [InlineData(new[] { StageStatus.Succeeded, StageStatus.Failed }, StageStatus.Failed)]
    [InlineData(new[] { StageStatus.Succeeded, StageStatus.Unavailable }, StageStatus.Unavailable)]
    [InlineData(new[] { StageStatus.Succeeded, StageStatus.Degraded }, StageStatus.Degraded)]
    [InlineData(new[] { StageStatus.Succeeded, StageStatus.Running }, StageStatus.Running)]
    [InlineData(new[] { StageStatus.Succeeded, StageStatus.Skipped }, StageStatus.Succeeded)]
    [InlineData(new[] { StageStatus.Skipped, StageStatus.Skipped }, StageStatus.Skipped)]
    public void TheRollUpIsTheWorstTopLevelStatus(StageStatus[] statuses, StageStatus expected)
    {
        var model = NewModel();
        for (var index = 0; index < statuses.Length; index++)
        {
            model.Apply(Stage(StageSource.Substrate, $"stage-{index}", statuses[index], T0.AddSeconds(index)));
        }

        var block = model.Blocks.Single(candidate => candidate.Source == StageSource.Substrate);
        block.RollUpStatus.ShouldBe(expected);
        block.RollUpText.ShouldBe(AgentPanelCopy.StageStatusText(expected));
        block.RollUpIconKey.ShouldBe(AgentPanelCopy.StageStatusIcon(expected));
        block.EmptyText.ShouldBeNull();
    }

    /// <summary>Each pill reads as the UI spec words it for each status.</summary>
    [Theory]
    [InlineData(StageSource.Ultrathink, StageStatus.Pending, "Ultrathink waiting")]
    [InlineData(StageSource.Ultrathink, StageStatus.Running, "Ultrathink planning\u2026")]
    [InlineData(StageSource.Ultrathink, StageStatus.Succeeded, "Ultrathink planned")]
    [InlineData(StageSource.Ultrathink, StageStatus.Degraded, "Ultrathink fallback")]
    [InlineData(StageSource.Ultrathink, StageStatus.Failed, "Ultrathink failed")]
    [InlineData(StageSource.Ultrathink, StageStatus.Skipped, "Ultrathink off")]
    [InlineData(StageSource.Ultrathink, StageStatus.Unavailable, "Ultrathink unavailable")]
    [InlineData(StageSource.Swarm, StageStatus.Degraded, "Swarm degraded")]
    [InlineData(StageSource.Swarm, StageStatus.Skipped, "Swarm off")]
    [InlineData(StageSource.Swarm, StageStatus.Unavailable, "Swarm unavailable")]
    [InlineData(StageSource.Substrate, StageStatus.Running, "Substrate checking\u2026")]
    [InlineData(StageSource.Substrate, StageStatus.Succeeded, "Substrate ok")]
    [InlineData(StageSource.Substrate, StageStatus.Degraded, "Substrate degraded")]
    [InlineData(StageSource.Substrate, StageStatus.Unavailable, "Substrate unavailable")]
    [InlineData(StageSource.Gsd, StageStatus.Failed, "GSD failed")]
    [InlineData(StageSource.Gsd, StageStatus.Running, "GSD checking\u2026")]
    public void PillsReadAsTheSpecWordsThem(StageSource source, StageStatus status, string expected)
    {
        var model = NewModel();

        model.Apply(Stage(source, "stage", status, T0));

        model.Pills.Single(pill => pill.Source == source).Text.ShouldBe(expected);
        model.Pills.Single(pill => pill.Source == source).Status.ShouldBe(status);
    }

    /// <summary>The Swarm pill counts finished children of the total.</summary>
    [Fact]
    public void TheSwarmPillCountsFinishedChildren()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, T0));
        for (var index = 0; index < 5; index++)
        {
            var status = index < 3 ? StageStatus.Succeeded : StageStatus.Running;
            model.Apply(Stage(StageSource.Swarm, $"agent-{index}", status, T0.AddSeconds(index + 1), parent: "subagents"));
        }

        model.Pills.Single(pill => pill.Source == StageSource.Swarm).Text.ShouldBe("Swarm 3 of 5");

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Succeeded, T0.AddSeconds(30)));

        model.Pills.Single(pill => pill.Source == StageSource.Swarm).Text.ShouldBe("Swarm 5 done");
    }

    /// <summary>A swarm parent without children shows its own status.</summary>
    [Fact]
    public void ASwarmParentWithoutChildrenShowsItsOwnStatus()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Running, T0));
        model.Pills.Single(pill => pill.Source == StageSource.Swarm).Text.ShouldBe("Swarm running");

        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Succeeded, T0.AddSeconds(1)));
        model.Pills.Single(pill => pill.Source == StageSource.Swarm).Text.ShouldBe("Swarm done");
    }

    /// <summary>The GSD pill shows the phase once it is known and reads unknown before.</summary>
    [Fact]
    public void TheGsdPillShowsThePhaseWhenKnown()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Gsd, "phase", StageStatus.Succeeded, T0));
        model.Pills.Single(pill => pill.Source == StageSource.Gsd).Text.ShouldBe("GSD unknown");

        model.SetPhase(new GsdPhaseInfo { Number = "46", Name = "Agent Panel", Status = "in progress", PlansComplete = 2, PlansTotal = 5 });

        model.Pills.Single(pill => pill.Source == StageSource.Gsd).Text.ShouldBe("GSD 46 \u00B7 2 of 5");
        model.PipelineAccessibleName.ShouldContain("GSD 46 \u00B7 2 of 5");
    }

    /// <summary>The degraded count covers skipped, degraded, failed and unavailable plugin stages, not the engine and not healthy ones.</summary>
    [Fact]
    public void TheDegradedCountCoversPluginStagesOnly()
    {
        var model = NewModel();

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, T0));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Unavailable, T0));
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, T0));
        model.Apply(Stage(StageSource.Omp, "engine", StageStatus.Failed, T0));
        model.Apply(Stage(StageSource.Swarm, "agent", StageStatus.Failed, T0, parent: "subagents"));

        model.DegradedStageCount.ShouldBe(2);
        model.AllPluginsUnavailable.ShouldBeFalse();
    }

    /// <summary>When all four plugin sources report unavailable, the panel knows the agent runs as plain omp.</summary>
    [Fact]
    public void AllPluginsUnavailableNeedsEveryPluginSourceToReport()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Unavailable, T0));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Unavailable, T0));
        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Unavailable, T0));
        model.AllPluginsUnavailable.ShouldBeFalse();

        model.Apply(Stage(StageSource.Gsd, "phase", StageStatus.Unavailable, T0));

        model.AllPluginsUnavailable.ShouldBeTrue();
        model.DegradedStageCount.ShouldBe(4);
    }

    /// <summary>The run summary follows the spec sentence and adds the recorded steering count to the Substrate clause.</summary>
    [Fact]
    public void TheRunSummaryFollowsTheSpecSentence()
    {
        var model = NewModel();
        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, T0));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Succeeded, T0));
        for (var index = 0; index < 5; index++)
        {
            model.Apply(Stage(StageSource.Swarm, $"agent-{index}", StageStatus.Succeeded, T0.AddSeconds(index), parent: "subagents"));
        }

        model.Apply(Stage(StageSource.Substrate, "brief", StageStatus.Succeeded, T0));
        model.Apply(Stage(StageSource.Gsd, "phase", StageStatus.Succeeded, T0));
        model.SetPhase(new GsdPhaseInfo { Number = "46", Name = "Agent Panel", Status = "in progress", PlansComplete = 2, PlansTotal = 5 });

        model.BuildRunSummary(2).ShouldBe("Ultrathink: planned. Swarm: 5 of 5 done. Substrate: ok, 2 steering messages recorded. GSD: 46, plan 2 of 5.");
        model.BuildRunSummary(1).ShouldContain("Substrate: ok, 1 steering message recorded.");
        model.BuildRunSummary(0).ShouldContain("Substrate: ok.");
    }

    /// <summary>Skipped and unavailable sources read off and unavailable, and sources with no events are left out.</summary>
    [Fact]
    public void TheRunSummaryNamesOffAndUnavailableSourcesAndSkipsSilentOnes()
    {
        var model = NewModel();
        model.BuildRunSummary(0).ShouldBeEmpty();

        model.Apply(Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, T0));
        model.Apply(Stage(StageSource.Swarm, "subagents", StageStatus.Unavailable, T0));

        model.BuildRunSummary(3).ShouldBe("Ultrathink: off. Swarm: unavailable.");
    }

    private static IReadOnlyList<StageSource> PipelineOrder { get; } =
        [StageSource.Omp, StageSource.Ultrathink, StageSource.Swarm, StageSource.Substrate, StageSource.Gsd];

    private static Task NoRecheckAsync(StageSource source, CancellationToken cancellationToken) => Task.CompletedTask;

    private static StageTimelineModel NewModel(Func<StageSource, CancellationToken, Task>? recheck = null) =>
        new(SessionId, new ManualTimeProvider(), recheck);

    private static IReadOnlyList<StageRow> Rows(StageTimelineModel model, StageSource source) =>
        model.Blocks.Single(block => block.Source == source).Rows;
}
