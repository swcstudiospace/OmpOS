using System.Net;
using System.Text;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Approvals;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.State;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The workbench over the Demo handler on a manual clock: start, selection, a full turn, steering, tool approvals, refused sends and retry,
/// close and reopen, the Changes bar, the user event stream, and disposal. No network, no real time.
/// </summary>
public sealed class WebWorkbenchTests : IDisposable
{
    private readonly DemoTestHost _host = new();

    /// <summary>Start loads the six sessions, the workspace and the source, selects the first session and shows the board.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task StartLoadsTheWorkspaceAndSelectsTheMostRecentSession()
    {
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);

        await workbench.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        workbench.IsDemo.ShouldBeTrue();
        workbench.Panel.State.ShouldBe(PanelState.Ready);
        workbench.Panel.SessionCount.ShouldBe(6);
        workbench.Panel.Selected.ShouldNotBeNull().Id.ShouldBe(DemoFixtures.RunningId);
        workbench.Workspace.ShouldNotBeNull().Name.ShouldBe("acme-shop");
        workbench.Source.ShouldNotBeNull().Revision.ShouldBe("demo");
        workbench.Board.State.ShouldBe(BoardState.Ready);
        workbench.Board.Columns.Count.ShouldBe(10);
        WebWorkbench.TerminalAvailability.IsAvailable.ShouldBeFalse();
        workbench.Panel.Catalog.Sessions.Select(static row => row.Status).ShouldBe(
            [
                AgentRowStatus.NeedsApproval,
                AgentRowStatus.Running,
                AgentRowStatus.Running,
                AgentRowStatus.Closed,
                AgentRowStatus.Completed,
                AgentRowStatus.Failed,
            ]);
    }

    /// <summary>An API that cannot be reached puts the panel in its Error state with a reason, and Retry loads again.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UnreachableApiShowsThePanelErrorAndRetryRecovers()
    {
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        _host.Interceptor.Intercept = request => string.Equals(request.RequestUri!.AbsolutePath, "/sessions", StringComparison.Ordinal) ? throw new HttpRequestException("offline") : null;

        await workbench.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        workbench.Panel.State.ShouldBe(PanelState.Error);
        workbench.Panel.BannerText.ShouldNotBeNull().ShouldContain(WebFailureText.Unreachable);
        _host.Interceptor.Intercept = null;
        await workbench.Panel.RetryLoadingAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        workbench.Panel.State.ShouldBe(PanelState.Ready);
        workbench.Panel.SessionCount.ShouldBe(6);
    }

    /// <summary>A prompt in a new chat streams the scripted reply at the typing cadence, then the composer is idle again and the rail shows Completed.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task APromptStreamsTheScriptedReplyAndReleasesTheComposer()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        await workbench.Panel.NewSessionAsync(ct).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => session.Session.State != SessionViewState.LoadingHistory).ConfigureAwait(true);

        session.Composer.Draft = "hello there";
        await session.Composer.SendAsync().ConfigureAwait(true);

        session.Composer.IsStreaming.ShouldBeTrue();
        session.Composer.Draft.ShouldBeEmpty("the accepted prompt leaves the draft");
        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.Success && !session.Composer.IsStreaming).ConfigureAwait(true);
        var blocks = session.Session.Tree.Blocks;
        blocks.OfType<UserMessageBlock>().Single().Text.ShouldBe("hello there");
        blocks.OfType<MarkdownBlock>().Any(static block => block.Markdown.Contains("Demo", StringComparison.Ordinal)).ShouldBeTrue("the answer is labelled as scripted");
        blocks.OfType<CodeBlock>().ShouldNotBeEmpty();
        _host.World.Find(session.Id).Status.ShouldBe(SessionStatus.Completed);
        await _host.PumpUntilAsync(() => workbench.Panel.Catalog.Sessions.Single(row => string.Equals(row.Id, session.Id, StringComparison.Ordinal)).Status == AgentRowStatus.Completed).ConfigureAwait(true);
    }

    /// <summary>A steering message while a turn runs is delivered into the turn, shown in the steering history, and acknowledged in the transcript.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SteeringDuringARunIsDeliveredAndAcknowledged()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        await workbench.Panel.NewSessionAsync(ct).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => session.Session.State != SessionViewState.LoadingHistory).ConfigureAwait(true);
        session.Composer.Draft = "start";
        await session.Composer.SendAsync().ConfigureAwait(true);

        var outcome = await session.Steering.SubmitAsync("keep the change small", ct).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.Accepted);
        session.Steering.History.Single().DeliveryStatus.ShouldBe(SteeringDeliveryStatus.DeliveredIntoTurn);
        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.Success && !session.Composer.IsStreaming).ConfigureAwait(true);
        session.Session.Tree.Blocks.OfType<MarkdownBlock>().Any(static block => block.Markdown.Contains("Noted your message", StringComparison.Ordinal)).ShouldBeTrue();
    }

    /// <summary>Steering a session that runs nothing is refused with the reason, and no exception reaches the UI.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SteeringWithoutARunIsAnOutcomeNotAnException()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.CompletedId).ShouldBeTrue();
        var session = workbench.Panel.Selected.ShouldNotBeNull();

        var outcome = await session.Steering.SubmitAsync("one more thing", ct).ConfigureAwait(true);

        outcome.ShouldBe(SteeringSubmitOutcome.NoActiveRun);
    }

    /// <summary>A pending tool approval is listed, allowed through the coordinator, and the turn then completes.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ToolApprovalIsListedDecidedAndTheTurnContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.ApprovalId).ShouldBeTrue();
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => session.PendingApprovals.Count == 1).ConfigureAwait(true);

        var approval = session.PendingApprovals.Single();
        approval.RequestId.ShouldBe(DemoFixtures.ApprovalRequestId);
        approval.ToolName.ShouldBe("bash");
        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.AwaitingApproval).ConfigureAwait(true);

        await session.ApprovalCoordinator.ShouldNotBeNull().DecideAsync(approval, ToolApprovalDecision.Allow, ct).ConfigureAwait(true);

        approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
        await _host.PumpUntilAsync(() => _host.World.Find(DemoFixtures.ApprovalId).Status == SessionStatus.Completed).ConfigureAwait(true);
        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.Success).ConfigureAwait(true);
        _host.World.Find(DemoFixtures.ApprovalId).Approvals.ShouldBeEmpty();
    }

    /// <summary>A refused send shows a recoverable error, keeps the draft and the composer idle; Retry sends the prompt again.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RefusedSendShowsARecoverableErrorAndRetrySucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        await workbench.Panel.NewSessionAsync(ct).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => session.Session.State != SessionViewState.LoadingHistory).ConfigureAwait(true);
        _host.Interceptor.Intercept = request => request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/prompts", StringComparison.Ordinal)
            ? new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("""{"title":"Too many requests.","status":429}""", Encoding.UTF8, "application/problem+json") }
            : null;

        session.Composer.Draft = "please try";
        await session.Composer.SendAsync().ConfigureAwait(true);

        session.Session.State.ShouldBe(SessionViewState.RecoverableError);
        session.Session.LastError.ShouldNotBeNull().ShouldContain("Too many requests");
        session.Composer.IsStreaming.ShouldBeFalse();
        session.Composer.Draft.ShouldBe("please try");
        _host.Interceptor.Intercept = null;

        await workbench.RetryAsync(session.Id).ConfigureAwait(true);

        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.Success && !session.Composer.IsStreaming).ConfigureAwait(true);
        _host.World.Find(session.Id).Status.ShouldBe(SessionStatus.Completed);
    }

    /// <summary>Closing a session marks it closed everywhere; reopening brings it back.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CloseAndReopenReachTheApiAndTheRail()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.CompletedId).ShouldBeTrue();
        var session = workbench.Panel.Selected.ShouldNotBeNull();

        await workbench.Panel.CloseSessionAsync(DemoFixtures.CompletedId, ct).ConfigureAwait(true);

        session.IsClosed.ShouldBeTrue();
        _host.World.Find(DemoFixtures.CompletedId).Closed.ShouldBeTrue();
        workbench.Panel.Catalog.Sessions.Single(static row => string.Equals(row.Id, DemoFixtures.CompletedId, StringComparison.Ordinal)).Status.ShouldBe(AgentRowStatus.Closed);

        await session.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);

        session.IsClosed.ShouldBeFalse();
        _host.World.Find(DemoFixtures.CompletedId).Closed.ShouldBeFalse();
    }

    /// <summary>The Changes bar lists the three changed files; Keep and Undo reach the API and a Demo notice says no file was written.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ChangesBarKeepsAndUndoesThroughTheApi()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.CompletedId).ShouldBeTrue();
        await _host.PumpUntilAsync(() => workbench.ChangesOf(DemoFixtures.CompletedId)?.Entries.Count == 3).ConfigureAwait(true);
        var changes = workbench.ChangesOf(DemoFixtures.CompletedId).ShouldNotBeNull();

        changes.Keep("src/Products/ProductsController.cs").Outcome.ShouldBe(ChangeOutcome.Kept);
        changes.Undo("src/Products/PagedResult.cs").Outcome.ShouldBe(ChangeOutcome.Undone);

        await _host.PumpUntilAsync(() => _host.World.Find(DemoFixtures.CompletedId).Changes.Count(static change => change.Status != Autonogrammer.Contracts.Controls.ChangeReviewStatus.Pending) == 2).ConfigureAwait(true);
        var stored = _host.World.Find(DemoFixtures.CompletedId).Changes.ToDictionary(static change => change.Path, static change => change.Status, StringComparer.Ordinal);
        stored["src/Products/ProductsController.cs"].ShouldBe(Autonogrammer.Contracts.Controls.ChangeReviewStatus.Accepted);
        stored["src/Products/PagedResult.cs"].ShouldBe(Autonogrammer.Contracts.Controls.ChangeReviewStatus.Rejected);
        stored["tests/Products/ProductsPagingTests.cs"].ShouldBe(Autonogrammer.Contracts.Controls.ChangeReviewStatus.Pending);
        await _host.PumpUntilAsync(() => workbench.ChangeNoticeOf(DemoFixtures.CompletedId) is not null).ConfigureAwait(true);
        workbench.ChangeNoticeOf(DemoFixtures.CompletedId).ShouldNotBeNull().ShouldContain("Demo");
    }

    /// <summary>The scripted turn of the running session streams markdown, a code block, tool calls and two diffs, fills the stage timeline and the Changes bar, and completes.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TheRunningSessionStreamsTextCodeToolCallsAndDiffs()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        session.Id.ShouldBe(DemoFixtures.RunningId);
        _host.World.StartRunningSession().ShouldBeTrue();

        await _host.PumpUntilAsync(() => session.Session.State == SessionViewState.Success && !session.Composer.IsStreaming).ConfigureAwait(true);

        var blocks = session.Session.Tree.Blocks;
        blocks.OfType<ToolCallBlock>().ShouldNotBeEmpty();
        blocks.OfType<ToolResultBlock>().ShouldNotBeEmpty();
        blocks.OfType<CodeBlock>().ShouldNotBeEmpty();
        blocks.OfType<FileEditBlock>().Select(static edit => edit.Path).ShouldBe(new[] { DemoScripts.TotalsPath, DemoScripts.TotalsTestPath });
        await _host.PumpUntilAsync(() => workbench.ChangesOf(DemoFixtures.RunningId)?.Entries.Count == 2).ConfigureAwait(true);
        await _host.PumpUntilAsync(() => session.Timeline.StatusOf(StageSource.Omp) == StageStatus.Succeeded).ConfigureAwait(true);
    }

    /// <summary>An omp terminal session offers no model picker, mode chip or prompt, and says so when a message is sent.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TerminalSessionHasNoComposerControlsAndRefusesAPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.TerminalId).ShouldBeTrue();
        var session = workbench.Panel.Selected.ShouldNotBeNull();

        session.Row.IsTerminal.ShouldBeTrue();
        session.Composer.ModelPicker.ShouldBeNull();
        session.Composer.SlashPalette.ShouldBeNull();
        session.Composer.Draft = "hello";
        await session.Composer.SendAsync().ConfigureAwait(true);

        session.Composer.Draft.ShouldBe("hello");
        session.Timeline.StatusAnnouncement.ShouldNotBeNull().ShouldContain("terminal");
        _host.Recorder.Requests.ShouldNotContain(request => request.EndsWith("/prompts", StringComparison.Ordinal));
    }

    /// <summary>A change another tab makes arrives on the user event stream and updates the rail; a created session appears.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UserEventStreamKeepsTheRailCurrent()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);

        _ = _host.World.Close(DemoFixtures.FailedId);
        _ = _host.World.Create();

        await _host.PumpUntilAsync(() => workbench.Panel.Catalog.Sessions.Single(static row => string.Equals(row.Id, DemoFixtures.FailedId, StringComparison.Ordinal)).Status == AgentRowStatus.Closed).ConfigureAwait(true);
        await _host.PumpUntilAsync(() => workbench.Panel.SessionCount == 7).ConfigureAwait(true);
    }

    /// <summary>The model chip lists the Demo models; changing it between runs reaches the API, and while a turn runs the API refuses with a reason.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ModelPickerChangesBetweenRunsAndRefusesDuringOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var workbench = NewWorkbench();
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(ct).ConfigureAwait(true);
        workbench.Panel.SelectSession(DemoFixtures.CompletedId).ShouldBeTrue();
        var session = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => session.Composer.ModelPicker?.Choices.Count == DemoCatalogue.Models.Count).ConfigureAwait(true);
        var picker = session.Composer.ModelPicker.ShouldNotBeNull();
        var next = picker.Choices.Single(static choice => string.Equals(choice.Value, "openai/gpt-5-codex", StringComparison.Ordinal));

        await picker.SelectAsync(next).ConfigureAwait(true);

        picker.CurrentValue.ShouldBe("openai/gpt-5-codex");
        _host.World.Find(DemoFixtures.CompletedId).Options.Single(static option => string.Equals(option.Id, "model", StringComparison.Ordinal)).CurrentValue.ShouldBe("openai/gpt-5-codex");
        session.Composer.ModeChip.ShouldNotBeNull().Choices.Count.ShouldBe(2);

        workbench.Panel.SelectSession(DemoFixtures.ApprovalId).ShouldBeTrue();
        var busy = workbench.Panel.Selected.ShouldNotBeNull();
        await _host.PumpUntilAsync(() => busy.Composer.ModelPicker?.Choices.Count == DemoCatalogue.Models.Count).ConfigureAwait(true);
        busy.Composer.IsStreaming = false;
        await busy.Composer.ModelPicker.ShouldNotBeNull().SelectAsync(busy.Composer.ModelPicker.Choices[1]).ConfigureAwait(true);
        busy.Composer.ModelPicker.Notice.ShouldNotBeNull().ShouldContain("busy");
    }

    /// <summary>Disposing stops the event stream and the runtimes; a second dispose does nothing.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task DisposeIsIdempotentAndStopsTheStream()
    {
        var workbench = NewWorkbench();
        await workbench.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await workbench.DisposeAsync().ConfigureAwait(true);
        await workbench.DisposeAsync().ConfigureAwait(true);

        await Should.ThrowAsync<ObjectDisposedException>(() => workbench.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private WebWorkbench NewWorkbench() => new(_host.Api, _host.Feed, new NoJsRuntime(), _host.Time, new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo"), []);
}
