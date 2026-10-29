using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.State;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>One test per row of the States table of the UI spec: the copy each state renders, in the shell, from the presentation models' own text.</summary>
public sealed class AgentPanelStateTests
{
    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>With no agent the rail and the thread render the empty copy, and the composer is absent.</summary>
    [Fact]
    public async Task EmptyStateRendersItsCopy()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.Create();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var texts = ShellTree.VisibleTexts(main);

            texts.ShouldContain(AgentPanelCopy.EmptyRailHeading, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.EmptyRailBody, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.EmptyThreadHeading, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.EmptyThreadBody, StringComparer.Ordinal);
            main.FindControl<ContentControl>("PipelineHost")!.Content.ShouldBeNull("with no agent there is no plugin connection row");
            ShellTree.NamedVisible<Button>(main, "NewAgentButton").ShouldNotBeNull();
            ShellTree.NamedVisible<Button>(main, "StartFirstAgentButton").ShouldNotBeNull();
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse("with no agent there is no composer");
            main.FindControl<ContentControl>("ThreadHeaderHost")!.Content.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While the panel initialises it renders the loading copy and stays usable.</summary>
    [Fact]
    public async Task LoadingStateRendersItsCopy()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.Create();
            using var initialising = fixture.Panel.BeginInitialising();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var texts = ShellTree.VisibleTexts(main);

            texts.ShouldContain(AgentPanelCopy.PanelLoadingHeading, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.PanelLoadingBody, StringComparer.Ordinal);
            ShellHarness.NewAgent(window).IsEnabled.ShouldBeTrue("a loading panel stays usable");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A panel load failure renders the assertive Panel error and the rail error copy, and Retry loading reloads through the host.</summary>
    [Fact]
    public async Task PanelErrorOffersRetry()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            fixture.Panel.ReportLoadFailure("the registry is locked");
            ShellHarness.Settle(window);

            var error = ShellTree.NamedVisible<Border>(main, "PanelError").ShouldNotBeNull();
            Avalonia.Automation.AutomationProperties.GetLiveSetting(error).ShouldBe(Avalonia.Automation.AutomationLiveSetting.Assertive);
            ShellTree.VisibleTexts(main).ShouldContain("Could not load your agents. the registry is locked. Your saved agents are not lost.", StringComparer.Ordinal);
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.RailErrorHeading, StringComparer.Ordinal);
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.RailErrorBody, StringComparer.Ordinal);

            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "RetryLoadingButton")!);
            await ShellWait.UntilAsync(window, () => fixture.Host.ReloadCount == 1 && fixture.Panel.State == PanelState.Empty, "the reload to finish", token).ConfigureAwait(true);

            ShellTree.NamedVisible<Border>(main, "PanelError").ShouldBeNull("a successful reload clears the error");
            await fixture.DisposeAsync().ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>While a run streams, Send becomes Steer, Queue follow-up shows, Stop stays enabled, and the steering badge reads Steering available.</summary>
    [Fact]
    public async Task StreamingShowsStopAndSteer()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                Prompts = new ScriptedPromptChannel(Working),
                RecordSteering = static (_, _) => Task.CompletedTask,
            };
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");

            await SendAsync(window, "long task").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Session.State == SessionViewState.Streaming, "the transcript to stream", token).ConfigureAwait(true);

            Avalonia.Automation.AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Steer message");
            var followUp = ShellHarness.Composer(window).FindControl<Button>("FollowUpButton")!;
            followUp.IsVisible.ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(followUp).ShouldBe("Queue follow-up message");
            ShellHarness.Stop(window).IsEnabled.ShouldBeTrue();
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.SteeringAvailable, StringComparer.Ordinal);

            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            Avalonia.Automation.AutomationProperties.GetName(ShellHarness.Send(window)).ShouldBe("Send message", "an idle composer sends again");
            followUp.IsVisible.ShouldBeFalse();
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.SteeringIdle, StringComparer.Ordinal);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failed turn shows the assertive engine error and the transcript's Retry turn control, which sends the last prompt again.</summary>
    [Fact]
    public async Task TurnErrorOffersRetry()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var channel = new ScriptedPromptChannel(
                new ScriptedTurn([], Fault: "model link down"),
                new ScriptedTurn([new TextDeltaEvent("recovered"), new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = channel });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");

            await SendAsync(window, "do the task").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            var banner = main.FindControl<TextBlock>("ErrorBanner")!;
            banner.IsVisible.ShouldBeTrue();
            banner.Text.ShouldBe($"{MainView.EngineFailedBanner} model link down");
            Avalonia.Automation.AutomationProperties.GetLiveSetting(banner).ShouldBe(Avalonia.Automation.AutomationLiveSetting.Assertive);
            var retry = ShellTree.NamedVisible<Button>(ShellHarness.Transcript(window), "SessionRetryButton");
            retry.ShouldNotBeNull("the transcript offers Retry turn after a recoverable failure or fault");

            runtime.RetryLastTurn();
            await ShellWait.UntilAsync(window, () => channel.Prompts.Count == 2, "the retry to send the prompt again", token).ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            channel.Prompts.ShouldBe(["do the task", "do the task"]);
            banner.IsVisible.ShouldBeFalse("a new send clears the banner of the earlier failure");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A finished run shows the run summary on the Pipeline tab and announces that the agent finished.</summary>
    [Fact]
    public async Task SuccessShowsStageSummary()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = SessionScript.WithTurns(new ScriptedTurn([new TextDeltaEvent("done"), new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            script.Stages.Emit(Stage(StageSource.Ultrathink, "plan", StageStatus.Succeeded, "Planned", "8 nodes, 4 waves, 2 clarifications"));
            await ShellWait.UntilAsync(window, () => runtime.ViewModel.Timeline.HasEvents, "the stage to fold", token).ConfigureAwait(true);

            await SendAsync(window, "do it").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Session.State == SessionViewState.Success, "the run to succeed", token).ConfigureAwait(true);

            var summary = runtime.ViewModel.RunSummaryText.ShouldNotBeNull();
            summary.ShouldStartWith("Finished.");
            ShellTree.VisibleTexts(OpenStageRows(window)).ShouldContain(summary, StringComparer.Ordinal, "the stage rows flyout starts with the run summary");
            ShellTree.NamedVisible<TextBlock>(main, "Announcement")!.Text.ShouldNotBeNull().ShouldContain(AgentPanelCopy.AgentFinished, Case.Insensitive);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ultrathink turned off: the pill, the effect line and the panel notice say so.</summary>
    [Fact]
    public async Task UltrathinkOffDegrades() =>
        await DegradedAsync(
            [Stage(StageSource.Ultrathink, "plan", StageStatus.Skipped, "Ultrathink")],
            "Ultrathink off",
            "Planning is off. Prompts run as written.",
            degradedCount: 1).ConfigureAwait(true);

    /// <summary>The planner fell back to a plain spec: the effect line says no graph was created.</summary>
    [Fact]
    public async Task UltrathinkFallbackDegrades() =>
        await DegradedAsync(
            [Stage(StageSource.Ultrathink, "plan", StageStatus.Degraded, "Ultrathink")],
            "Ultrathink fallback",
            "The planner fell back to a plain spec. No graph was created.",
            degradedCount: 1).ConfigureAwait(true);

    /// <summary>Swarm missing: the pill says unavailable and the effect line says the agent runs alone.</summary>
    [Fact]
    public async Task SwarmMissingRunsSingleAgent() =>
        await DegradedAsync(
            [Stage(StageSource.Swarm, "swarm", StageStatus.Unavailable, "Swarm")],
            "Swarm unavailable",
            "Swarm is not available. This agent runs alone.",
            degradedCount: 1).ConfigureAwait(true);

    /// <summary>An uninitialised submodule shows the exception text with the exact fix, verbatim.</summary>
    [Fact]
    public async Task UninitialisedSubmoduleShowsFix()
    {
        const string Fix = "Swarm is not set up. plugins/agent-swarm is empty. Run: git submodule update --init --recursive plugins/agent-swarm";
        await DegradedAsync(
            [Stage(StageSource.Swarm, "swarm", StageStatus.Unavailable, "Swarm", Fix)],
            "Swarm unavailable",
            Fix,
            degradedCount: 1).ConfigureAwait(true);
    }

    /// <summary>GSD tools missing: the pill, the effect line and the unknown phase say so.</summary>
    [Fact]
    public async Task GsdMissingShowsUnknownPhase() =>
        await DegradedAsync(
            [Stage(StageSource.Gsd, "gsd", StageStatus.Unavailable, "GSD")],
            "GSD unavailable",
            "GSD tools were not found. The workflow phase is unknown.",
            degradedCount: 1).ConfigureAwait(true);

    /// <summary>Substrate unreachable: the pill and effect line say so, and a steer is accepted but not recorded.</summary>
    [Fact]
    public async Task SubstrateUnavailableHidesRecord()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                Prompts = new ScriptedPromptChannel(Working),
                RecordSteering = static (_, _) => Task.CompletedTask,
            };
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            script.Stages.Emit(Stage(StageSource.Substrate, "substrate", StageStatus.Unavailable, "Substrate"));
            await ShellWait.UntilAsync(window, () => runtime.ViewModel.Timeline.HasEvents, "the stage to fold", token).ConfigureAwait(true);

            await SendAsync(window, "long task").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => runtime.Session.State == SessionViewState.Streaming, "the transcript to stream", token).ConfigureAwait(true);

            ShellTree.VisibleTexts(OpenStageRows(window)).ShouldContain("Substrate is not reachable. Steering is not recorded and shared memory is off.", StringComparer.Ordinal);
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.SteeringAvailableNotRecorded, StringComparer.Ordinal);
            runtime.ViewModel.Timeline.PipelineAccessibleName.ShouldContain("Substrate unavailable");

            await runtime.ViewModel.Steering.SubmitAsync("steer left", SteeringMode.Steer, token).ConfigureAwait(true);

            runtime.ViewModel.Steering.History.ShouldHaveSingleItem().RecordText.ShouldBe(AgentPanelCopy.RecordNotRecorded);
            await runtime.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Every plugin source unavailable: the panel says the agent runs as plain omp, and the agent still runs.</summary>
    [Fact]
    public async Task NoPluginsStillRuns()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = SessionScript.WithTurns(new ScriptedTurn([new TextDeltaEvent("plain"), new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            script.Stages.Emit(Stage(StageSource.Ultrathink, "plan", StageStatus.Unavailable, "Ultrathink"));
            script.Stages.Emit(Stage(StageSource.Swarm, "swarm", StageStatus.Unavailable, "Swarm"));
            script.Stages.Emit(Stage(StageSource.Substrate, "substrate", StageStatus.Unavailable, "Substrate"));
            script.Stages.Emit(Stage(StageSource.Gsd, "gsd", StageStatus.Unavailable, "GSD"));
            await ShellWait.UntilAsync(window, () => runtime.ViewModel.Timeline.AllPluginsUnavailable, "every plugin to report", token).ConfigureAwait(true);

            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.NoPlugins, StringComparer.Ordinal);
            ShellTree.NamedVisible<Button>(main, "ShowPipelineButton").ShouldNotBeNull();

            await SendAsync(window, "no plugins").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);

            script.Scripted.Prompts.ShouldBe(["no plugins"], "the session runs as plain omp");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failed stage shows its effect line and Retry stage, re-checks only that source, and never blocks the panel.</summary>
    [Fact]
    public async Task StageErrorDoesNotBlockThePanel()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = SessionScript.WithTurns(new ScriptedTurn([new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");
            script.Stages.Emit(Stage(StageSource.Swarm, "swarm", StageStatus.Failed, "Swarm", "the live file is unreadable"));
            await ShellWait.UntilAsync(window, () => runtime.ViewModel.Timeline.HasEvents, "the stage to fold", token).ConfigureAwait(true);

            var rows = OpenStageRows(window);
            ShellTree.VisibleTexts(rows).ShouldContain("Swarm stopped. Work continues as a single agent.", StringComparer.Ordinal);
            var retry = FindRetryStageButton(rows);
            retry.ShouldNotBeNull("a failed stage offers Retry stage");
            ShellHarness.Click(window, retry);
            await ShellWait.UntilAsync(window, () => fixture.Host.Rechecked.Count == 1, "the source to be re-checked", token).ConfigureAwait(true);

            fixture.Host.Rechecked.ShouldBe([("session-1", StageSource.Swarm)], "only the failed source is re-checked");
            await SendAsync(window, "still works").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
            script.Scripted.Prompts.ShouldBe(["still works"], "a failed stage never blocks the panel");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A stage source that faults becomes a degraded engine stage and the agent keeps running.</summary>
    [Fact]
    public async Task AFaultingStageSourceBecomesADegradedEngineStage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = new SessionScript
            {
                Prompts = new ScriptedPromptChannel(new ScriptedTurn([new CompletedEvent("end_turn")])),
                Stages = new ScriptedStageSource(),
            };
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var runtime = fixture.Runtime("session-1");
            script.Stages.Fault(new InvalidOperationException("the observer crashed"));

            await ShellWait.UntilAsync(
                window,
                () => runtime.ViewModel.Timeline.Blocks.Any(block => block.Source == StageSource.Omp && block.RollUpStatus == StageStatus.Degraded),
                "the engine stage to degrade",
                token).ConfigureAwait(true);

            await SendAsync(window, "still runs").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
            script.Scripted.Prompts.ShouldBe(["still runs"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A running agent that is old and quiet shows its Long-running and Quiet for flags in the rail.</summary>
    [Fact]
    public async Task LongRunningRowShowsFlags()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            fixture.Sessions.Put(new SessionSummary
            {
                Id = "old",
                Title = "Old agent",
                Lifecycle = SessionLifecycle.Running,
                CreatedAt = fixture.Clock.Now - TimeSpan.FromHours(3),
                LastActivity = fixture.Clock.Now - TimeSpan.FromMinutes(7),
            });
            fixture.Catalog.Refresh();
            var window = ShellHarness.Show(fixture);

            var row = fixture.Catalog.Sessions.Single(candidate => string.Equals(candidate.Id, "old", StringComparison.Ordinal));
            row.IsLongRunning.ShouldBeTrue();
            row.IsStale.ShouldBeTrue();
            var item = RowOf(window, "Old agent");
            var texts = ShellTree.VisibleTexts(item);
            texts.ShouldContain("Long-running", StringComparer.Ordinal);
            texts.ShouldContain(row.QuietText!, StringComparer.Ordinal);
            AutomationName(item).ShouldContain("long-running", Case.Insensitive);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A lost agent is in the Lost group with a Resume agent control and no composer; resuming brings it back and focuses its prompt.</summary>
    [Fact]
    public async Task LostSessionCanBeResumed()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.Create();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("lost", "Lost agent", SessionLifecycle.Lost);
            fixture.Panel.SelectSession("lost");
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            fixture.Catalog.Sessions.Single().StatusText.ShouldBe("Lost");
            ShellTree.VisibleTexts(main).ShouldContain($"{AgentPanelCopy.LostHeading} {AgentPanelCopy.LostBody}", StringComparer.Ordinal);
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse();
            var resume = ShellTree.NamedVisible<Button>(main, "ResumeButton").ShouldNotBeNull();

            ShellHarness.Click(window, resume);
            await ShellWait.UntilAsync(window, () => !fixture.Runtime("lost").ViewModel.IsLost, "the agent to resume", token).ConfigureAwait(true);

            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeTrue();
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.Resumed, StringComparer.Ordinal);
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("focus moves to the prompt after Resume agent");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A closed agent shows its notice, no composer and a steering badge of unavailable.</summary>
    [Fact]
    public async Task ClosedSessionShowsNotice()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);

            await fixture.Runtime("session-1").ViewModel.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => fixture.Runtime("session-1").ViewModel.IsClosed, "the agent to close", token).ConfigureAwait(true);

            var texts = ShellTree.VisibleTexts(main);
            texts.ShouldContain(AgentPanelCopy.ClosedHeading, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.ClosedBody, StringComparer.Ordinal);
            texts.ShouldContain(AgentPanelCopy.SteeringUnavailable, StringComparer.Ordinal);
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With many agents the rail scrolls and the selected row is brought into view.</summary>
    [Fact]
    public async Task DenseListScrollsSelectedRowIntoView()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            for (var index = 0; index < 40; index++)
            {
                fixture.AddSession($"agent-{index}", $"Agent number {index}");
            }

            var window = ShellHarness.Show(fixture, height: 640);
            fixture.Panel.SelectSession("agent-0");
            ShellHarness.Settle(window);

            var rows = ShellHarness.Rows(window);
            var selected = rows.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(item => item.IsSelected);
            selected.ShouldNotBeNull("the selected row is realized");
            var top = selected.TranslatePoint(new Point(0, 0), rows);
            top.ShouldNotBeNull();
            top.Value.Y.ShouldBeGreaterThanOrEqualTo(0);
            (top.Value.Y + selected.Bounds.Height).ShouldBeLessThanOrEqualTo(rows.Bounds.Height + 1, "the selected row lies inside the list's viewport");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A very long title stays on one line with an ellipsis in the row, and the row's accessible name keeps the text.</summary>
    [Fact]
    public async Task LongTextIsCapped()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.Dispatch(() =>
        {
            var fixture = ShellFixture.CreateWithSession();
            var title = "Investigate " + new string('x', 150) + " end";
            fixture.AddSession("long", title);
            var window = ShellHarness.Show(fixture);

            var item = RowOf(window, "Investigate");
            AutomationName(item).ShouldStartWith(title, Case.Sensitive, "the accessible name keeps the full title");
            var titleText = item.GetVisualDescendants().OfType<TextBlock>().First(text => string.Equals(text.Text, title, StringComparison.Ordinal));
            titleText.TextTrimming.ShouldBe(TextTrimming.CharacterEllipsis);
            titleText.MaxLines.ShouldBe(1);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task DegradedAsync(StageEvent[] events, string pill, string effect, int degradedCount)
    {
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var script = SessionScript.WithTurns(new ScriptedTurn([new CompletedEvent("end_turn")]));
            var fixture = ShellFixture.CreateWithSession(script);
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var runtime = fixture.Runtime("session-1");

            foreach (var stageEvent in events)
            {
                script.Stages.Emit(stageEvent);
            }

            await ShellWait.UntilAsync(window, () => runtime.ViewModel.Timeline.DegradedStageCount == degradedCount, "the degraded stages to fold", token).ConfigureAwait(true);

            runtime.ViewModel.Timeline.PipelineAccessibleName.ShouldContain(pill);
            ShellTree.VisibleTexts(OpenStageRows(window)).ShouldContain(effect, StringComparer.Ordinal);
            ShellTree.VisibleTexts(main).ShouldContain(AgentPanelCopy.DegradedNotice(degradedCount), StringComparer.Ordinal);
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeTrue("a degraded stage never takes the composer away");

            await SendAsync(window, "carry on").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, runtime, token).ConfigureAwait(true);
            script.Scripted.Prompts.ShouldBe(["carry on"], "the agent keeps running");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static StageEvent Stage(StageSource source, string id, StageStatus status, string title, string? detail = null) => new()
    {
        SessionId = "session-1",
        Source = source,
        StageId = id,
        Title = title,
        Status = status,
        Timestamp = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
        Detail = detail,
    };

    // The stage rows live in the flyout of the pipeline strip: opening it is how a user reaches the detail text, the fix text and Retry stage.
    private static PipelineTabView OpenStageRows(MainWindow window)
    {
        var strip = ShellHarness.MainViewOf(window).FindControl<ContentControl>("PipelineHost")!.Content.ShouldBeOfType<PipelineStripView>();
        strip.ShowDetails();
        ShellHarness.Settle(window);
        return strip.FindControl<PipelineTabView>("Details")!;
    }

    private static Button? FindRetryStageButton(Control root) =>
        root.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            button.IsEffectivelyVisible
            && AutomationName(button).StartsWith("Retry ", StringComparison.Ordinal)
            && AutomationName(button).EndsWith(" stage", StringComparison.Ordinal));

    private static string AutomationName(Control control) => Avalonia.Automation.AutomationProperties.GetName(control) ?? string.Empty;

    private static ListBoxItem RowOf(MainWindow window, string titlePrefix) =>
        ShellHarness.Rows(window).GetVisualDescendants().OfType<ListBoxItem>().First(item => AutomationName(item).StartsWith(titlePrefix, StringComparison.Ordinal));

    private static async Task SendAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        ShellHarness.Prompt(window).Focus();
        ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }
}
