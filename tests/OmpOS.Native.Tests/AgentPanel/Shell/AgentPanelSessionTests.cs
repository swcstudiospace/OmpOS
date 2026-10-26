using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Tests.Parity;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Native.Views.RunControls;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>Concurrent sessions in the Agent Panel: each owns its transcript, composer, approvals and engine; selecting one rebinds the thread (AL-10).</summary>
public sealed class AgentPanelSessionTests
{
    private static readonly ScriptedTurn Reply = new([new TextDeltaEvent("a reply"), new CompletedEvent("end_turn")]);

    private static readonly ScriptedTurn Working = new([new TextDeltaEvent("working")], HoldUntilCancelled: true);

    /// <summary>Two sessions keep independent transcripts, composers, drafts and approvals, and selecting a session rebinds the thread to it.</summary>
    [Fact]
    public async Task SelectingASessionRebindsTheThread()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Reply));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two", script: SessionScript.WithTurns(Reply));
            var window = ShellHarness.Show(fixture);
            var one = fixture.Runtime("session-1");
            ShellHarness.Prompt(window).Text = "a draft for one";
            one.RunControls.Approvals.Add(new PendingToolApproval("session-1", "req-1", "write_file", "{}"));

            fixture.Panel.SelectSession("session-2");
            ShellHarness.Settle(window);
            var two = fixture.Runtime("session-2");

            ShellHarness.Transcript(window).DataContext.ShouldBeSameAs(two.Session, "the transcript follows the selection");
            ShellHarness.Composer(window).DataContext.ShouldBeSameAs(two.Composer, "the composer follows the selection");
            ShellHarness.Prompt(window).Text.ShouldBeNullOrEmpty("the draft of the first agent stays with the first agent");
            Host(window, "RunControlsHost").ShouldBeOfType<RunControlsView>().DataContext.ShouldBeSameAs(two.RunControls, "the approvals follow the selection");
            Host(window, "RunControlsHost").ShouldBeOfType<RunControlsView>().IsVisible.ShouldBeFalse("the second agent has no pending approval");
            Host(window, "ThreadHeaderHost").ShouldBeOfType<ThreadHeaderView>().DataContext.ShouldBeSameAs(two.ViewModel);
            Host(window, "PipelineHost").ShouldBeOfType<PipelineStripView>().DataContext.ShouldBeSameAs(two.ViewModel.Timeline);
            Host(window, "PipelineHost").ShouldBeOfType<PipelineStripView>().FindControl<PipelineTabView>("Details")!.DataContext.ShouldBeSameAs(two.ViewModel.Timeline, "the stage rows flyout follows the selection");

            await SendAsync(window, "second prompt").ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, two, token).ConfigureAwait(true);

            two.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["second prompt"]);
            one.Session.Tree.Blocks.OfType<UserMessageBlock>().ShouldBeEmpty("the first agent never saw the second agent's prompt");
            fixture.Script("session-2").Scripted.Prompts.ShouldBe(["second prompt"]);
            fixture.Script("session-1").Scripted.Prompts.ShouldBeEmpty();

            fixture.Panel.SelectSession("session-1");
            ShellHarness.Settle(window);

            ShellHarness.Transcript(window).DataContext.ShouldBeSameAs(one.Session);
            ShellHarness.Prompt(window).Text.ShouldBe("a draft for one", "the first agent's draft came back");
            var approvals = Host(window, "RunControlsHost").ShouldBeOfType<RunControlsView>();
            approvals.DataContext.ShouldBeSameAs(one.RunControls);
            approvals.IsVisible.ShouldBeTrue("the first agent's pending approval is shown again");
            Host(window, "ThreadHeaderHost").ShouldBeOfType<ThreadHeaderView>().DataContext.ShouldBeSameAs(one.ViewModel);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An agent that is not shown keeps running, keeps its transcript and shows its own status in the rail.</summary>
    [Fact]
    public async Task AnUnselectedSessionKeepsRunning()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two", script: SessionScript.WithTurns(Reply));
            var window = ShellHarness.Show(fixture);
            var one = fixture.Runtime("session-1");

            await SendAsync(window, "long task").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => one.Session.State == SessionViewState.Streaming, "the first agent to stream", token).ConfigureAwait(true);

            fixture.Panel.SelectSession("session-2");
            ShellHarness.Settle(window);
            fixture.Catalog.RefreshTimes();

            one.Composer.IsStreaming.ShouldBeTrue("the first agent keeps running while the second is shown");
            one.Runs.IsRunActive.ShouldBeTrue();
            fixture.Catalog.Sessions.Single(row => string.Equals(row.Id, "session-1", StringComparison.Ordinal)).StatusText.ShouldBe("Running");
            fixture.Catalog.Sessions.Single(row => string.Equals(row.Id, "session-2", StringComparison.Ordinal)).StatusText.ShouldNotBe("Running", StringComparer.Ordinal, "each row shows its own status");
            fixture.Script("session-1").Scripted.CancelCount.ShouldBe(0);

            await one.Composer.StopAsync().ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, one, token).ConfigureAwait(true);

            fixture.Script("session-1").Scripted.CancelCount.ShouldBe(1);
            one.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["long task"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing an agent that is not running stops its engine at once, keeps it in the rail as Closed and selects the next agent.</summary>
    [Fact]
    public async Task ClosingAnIdleSessionStopsItsEngineAndSelectsTheNext()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var window = ShellHarness.Show(fixture);

            await fixture.Runtime("session-1").ViewModel.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => fixture.Host.Closed.Count == 1, "the host to stop the engine", token).ConfigureAwait(true);

            fixture.Host.Closed.ShouldBe(["session-1"]);
            fixture.Panel.Selected!.Id.ShouldBe("session-2", "the next agent is selected");
            ShellTree.NamedVisible<Border>(ShellHarness.MainViewOf(window), "CloseConfirmBar").ShouldBeNull("an idle agent closes without a confirmation");
            fixture.Catalog.Sessions.Single(row => string.Equals(row.Id, "session-1", StringComparison.Ordinal)).StatusText.ShouldBe("Closed");

            fixture.Panel.SelectSession("session-1");
            ShellHarness.Settle(window);

            var main = ShellHarness.MainViewOf(window);
            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeFalse("a closed agent has no composer");
            main.FindControl<Border>("ClosedNotice")!.IsVisible.ShouldBeTrue();
            ShellTree.VisibleTexts(main.FindControl<Border>("ClosedNotice")!).ShouldBe([AgentPanelCopy.ClosedHeading, AgentPanelCopy.ClosedBody]);
            ShellTree.NamedVisible<Button>(main, "ReopenButton").ShouldNotBeNull("a closed agent offers Reopen agent");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reopening a closed agent brings its composer back and moves focus to its prompt.</summary>
    [Fact]
    public async Task ReopeningAClosedSessionRestoresTheComposerAndFocusesThePrompt()
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

            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "ReopenButton")!);
            await ShellWait.UntilAsync(window, () => !fixture.Runtime("session-1").ViewModel.IsClosed, "the agent to reopen", token).ConfigureAwait(true);

            main.FindControl<ContentControl>("ComposerHost")!.IsVisible.ShouldBeTrue();
            main.FindControl<Border>("ClosedNotice")!.IsVisible.ShouldBeFalse();
            ShellHarness.Prompt(window).IsFocused.ShouldBeTrue("focus moves to the prompt after Reopen agent");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Closing an agent that is running asks first; keeping it open changes nothing, and confirming stops its engine.</summary>
    [Fact]
    public async Task ClosingARunningSessionAsksBeforeStoppingItsEngine()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession(SessionScript.WithTurns(Working));
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Agent two");
            var window = ShellHarness.Show(fixture);
            var main = ShellHarness.MainViewOf(window);
            var one = fixture.Runtime("session-1");
            await SendAsync(window, "long task").ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => one.ViewModel.RequiresCloseConfirmation, "the agent to run", token).ConfigureAwait(true);

            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "CloseButton")!);

            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldNotBeNull("a running agent asks before it closes");
            ShellTree.VisibleTexts(main).ShouldContain(one.ViewModel.CloseConfirmationText, StringComparer.Ordinal);
            fixture.Host.Closed.ShouldBeEmpty();
            ShellTree.NamedVisible<Button>(main, "KeepAgentOpenButton")!.IsFocused.ShouldBeTrue("the safe default has focus");

            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "KeepAgentOpenButton")!);

            ShellTree.NamedVisible<Border>(main, "CloseConfirmBar").ShouldBeNull();
            fixture.Host.Closed.ShouldBeEmpty("keeping the agent open stops nothing");
            one.Composer.IsStreaming.ShouldBeTrue();
            ShellTree.NamedVisible<Button>(main, "CloseButton")!.IsFocused.ShouldBeTrue("focus returns to Close this agent");

            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "CloseButton")!);
            ShellHarness.Click(window, ShellTree.NamedVisible<Button>(main, "CloseAgentButton")!);
            await ShellWait.UntilAsync(window, () => fixture.Host.Closed.Count == 1, "the engine to stop", token).ConfigureAwait(true);
            await ShellWait.RunEndedAsync(window, one, token).ConfigureAwait(true);

            fixture.Host.Closed.ShouldBe(["session-1"]);
            fixture.Script("session-1").Scripted.CancelCount.ShouldBe(1, "closing a running agent stops its turn");
            fixture.Panel.Selected!.Id.ShouldBe("session-2");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The last agent closed leaves the panel Ready with that agent selected and offering Reopen: only a catalog with no agent at all is Empty.</summary>
    [Fact]
    public async Task ClosingTheLastSessionKeepsItInTheRailAsClosed()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var window = ShellHarness.Show(fixture);

            await fixture.Runtime("session-1").ViewModel.CloseCommand.ExecuteAsync(null).ConfigureAwait(true);
            await ShellWait.UntilAsync(window, () => fixture.Runtime("session-1").ViewModel.IsClosed, "the agent to close", token).ConfigureAwait(true);

            fixture.Panel.State.ShouldBe(PanelState.Ready);
            fixture.Panel.Selected!.Id.ShouldBe("session-1");
            ShellTree.NamedVisible<Button>(ShellHarness.MainViewOf(window), "ReopenButton").ShouldNotBeNull();
            fixture.Panel.PipelineAnnouncement.ShouldBe(AgentPanelCopy.ClosedAnnouncement("Agent one"));
            ShellTree.NamedVisible<TextBlock>(ShellHarness.MainViewOf(window), "Announcement")!.Text.ShouldNotBeNull().ShouldContain("closed", Case.Insensitive, "the closing is announced through the Pipeline announcements line");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>New agent creates a session, selects it and gives it its own runtime, transcript and composer.</summary>
    [Fact]
    public async Task NewAgentCreatesAndSelectsASessionWithItsOwnRuntime()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.Host.NewSessionScript = _ => SessionScript.WithTurns(Reply);
            var window = ShellHarness.Show(fixture);

            ShellHarness.Click(window, ShellHarness.NewAgent(window));
            await ShellWait.UntilAsync(window, () => string.Equals(fixture.Panel.Selected?.Id, "new-1", StringComparison.Ordinal), "the new agent to be selected", token).ConfigureAwait(true);

            var created = fixture.Runtime("new-1");
            ShellHarness.Transcript(window).DataContext.ShouldBeSameAs(created.Session);
            ShellHarness.Composer(window).DataContext.ShouldBeSameAs(created.Composer);
            created.Session.ShouldNotBeSameAs(fixture.Runtime("session-1").Session);
            fixture.Catalog.Sessions.Select(row => row.Id).ShouldBe(["new-1", "session-1"], ignoreOrder: true);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static object? Host(MainWindow window, string name) => ShellHarness.MainViewOf(window).FindControl<ContentControl>(name)!.Content;

    private static async Task SendAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        ShellHarness.Prompt(window).Focus();
        ShellHarness.Press(window, Key.Enter, PhysicalKey.Enter);
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }
}
