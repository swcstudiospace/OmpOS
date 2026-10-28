using Bunit;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Changes;
using OmpOS.UI.Components.Thread;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The thread: header, notices, pipeline, transcript states, stop confirmation, retry, approvals, the Changes bar and the composer, all from the Agent Panel view models (PX-09).</summary>
public sealed class ThreadViewTests : UiTestContext
{
    private async Task<(PanelHarness Harness, AgentSessionViewModel Session, IRenderedComponent<ThreadView> Cut)> OpenAsync(
        SessionLifecycle lifecycle = SessionLifecycle.Running,
        Action<ComponentParameterCollectionBuilder<ThreadView>>? configure = null,
        PanelBridge? bridge = null)
    {
        var harness = PanelHarness.With(("a", lifecycle, "Alpha"));
        harness.Host.Bridge = bridge;
        var session = await harness.SelectAsync("a").ConfigureAwait(true);
        session.Session.MarkHistoryLoaded();
        var cut = Render<ThreadView>(parameters =>
        {
            parameters.Add(p => p.Session, session);
            configure?.Invoke(parameters);
        });
        return (harness, session, cut);
    }

    /// <summary>The header names the agent, shows its status in words behind a decorative glyph, the workspace and branch, the steering badge, and a named Close control.</summary>
    [Fact]
    public async Task TheHeaderShowsTitleStatusWorkspaceBranchAndSteering()
    {
        var (harness, session, cut) = await OpenAsync(configure: parameters => parameters
            .Add(p => p.WorkspaceLabel, "demo")
            .Add(p => p.Branch, "main")).ConfigureAwait(true);
        using var owned = harness;

        cut.Find("header[aria-label='Agent header'] h2").TextContent.ShouldBe("Alpha");
        var chip = cut.Find(".ompos-status-chip");
        chip.TextContent.ShouldBe("Status: " + session.Row.StatusText);
        chip.QuerySelector("svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        cut.Find(".ompos-thread-workspace").TextContent.ShouldBe("Workspace: demo");
        cut.Find(".ompos-branch-chip").TextContent.ShouldBe("Branch: main");
        cut.Find(".ompos-steering-badge").TextContent.ShouldBe(session.SteeringBadgeText);
        cut.Find(".ompos-steering-badge").GetAttribute("title").ShouldBe(session.SteeringBadgeName);
        cut.Find("button[aria-label='Close this agent']").ShouldNotBeNull();
    }

    /// <summary>The transcript goes from initialising to empty to streaming to success, and each state has its words and its live announcement.</summary>
    [Fact]
    public async Task TheTranscriptMovesThroughItsStates()
    {
        var harness = PanelHarness.With(("a", SessionLifecycle.Running, "Alpha"));
        using var owned = harness;
        var session = await harness.SelectAsync("a").ConfigureAwait(true);
        var cut = Render<ThreadView>(parameters => parameters.Add(p => p.Session, session));
        cut.Find("p.ompos-loading").TextContent.ShouldContain("Loading conversation history", Case.Sensitive);

        session.Session.MarkHistoryLoaded();
        await cut.WaitForAssertionAsync(() => cut.Find("p.ompos-empty").TextContent.ShouldContain(AgentPanelCopy.EmptySessionHeading, Case.Sensitive)).ConfigureAwait(true);

        session.Session.ApplyEvent(PanelEvents.User(1, "Do it"));
        session.Session.ApplyEvent(PanelEvents.Text(2, "Working"));
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-streaming").TextContent.ShouldBe("Working")).ConfigureAwait(true);
        cut.Find(".ompos-announcement").GetAttribute("aria-live").ShouldBe("polite");
        cut.Find(".ompos-announcement").TextContent.ShouldBe("Assistant is responding.");
        cut.Find("[data-state='streaming']").ShouldNotBeNull();

        session.Session.ApplyEvent(PanelEvents.Completed(3));
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-run-summary[role='status']").TextContent.ShouldContain(AgentPanelCopy.AgentFinished, Case.Sensitive)).ConfigureAwait(true);
        cut.FindAll("section > button[aria-label='Stop response']").ShouldBeEmpty();
    }

    /// <summary>Stop asks first; keeping the run leaves it, and confirming stops it through the host.</summary>
    [Fact]
    public async Task StopAsksBeforeStopping()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.Session.ApplyEvent(PanelEvents.Text(1, "Working"));
        await cut.WaitForAssertionAsync(() => cut.Find("section > button[aria-label='Stop response']").ShouldNotBeNull()).ConfigureAwait(true);

        await cut.Find("section > button[aria-label='Stop response']").ClickAsync().ConfigureAwait(true);

        var bar = cut.Find(".ompos-confirm-bar[data-kind='stop']");
        bar.GetAttribute("aria-label").ShouldBe("Confirm stop");
        bar.TextContent.ShouldContain(AgentPanelCopy.StopConfirmation, Case.Sensitive);
        await cut.Find(".ompos-confirm-bar button:not(.ompos-btn--primary)").ClickAsync().ConfigureAwait(true);
        cut.FindAll(".ompos-confirm-bar").ShouldBeEmpty();
        harness.Host.Stops.ShouldBe(0);

        await cut.Find("section > button[aria-label='Stop response']").ClickAsync().ConfigureAwait(true);
        await cut.Find(".ompos-confirm-bar .ompos-btn--primary").ClickAsync().ConfigureAwait(true);

        harness.Host.Stops.ShouldBe(1);
        cut.FindAll(".ompos-confirm-bar").ShouldBeEmpty();
    }

    /// <summary>A retryable error is an assertive alert with Retry; Retry sends the last user message again and leaves the draft the user was typing.</summary>
    [Fact]
    public async Task RetrySendsTheLastPromptAndKeepsTheDraft()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.Session.ApplyEvent(PanelEvents.User(1, "Do it"));
        session.Session.ApplyEvent(PanelEvents.Error(2, "rate limited", retryable: true));
        session.Composer.Draft = "typing";
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-error[role='alert']").TextContent.ShouldContain("rate limited", Case.Sensitive)).ConfigureAwait(true);

        await cut.Find("section > button[aria-label='Retry failed operation']").ClickAsync().ConfigureAwait(true);

        harness.Host.Sent.ShouldBe(["Do it"]);
        session.Composer.Draft.ShouldBe("typing");
    }

    /// <summary>The host's own retry replaces the default when the page supplies one.</summary>
    [Fact]
    public async Task AHostRetryReplacesTheDefault()
    {
        var retries = 0;
        var (harness, session, cut) = await OpenAsync(configure: parameters => parameters.Add(p => p.OnRetry, () => retries++)).ConfigureAwait(true);
        using var owned = harness;
        session.Session.ApplyEvent(PanelEvents.User(1, "Do it"));
        session.Session.ApplyEvent(PanelEvents.Error(2, "boom", retryable: true));
        await cut.WaitForAssertionAsync(() => cut.Find("section > button[aria-label='Retry failed operation']").ShouldNotBeNull()).ConfigureAwait(true);

        await cut.Find("section > button[aria-label='Retry failed operation']").ClickAsync().ConfigureAwait(true);

        retries.ShouldBe(1);
        harness.Host.Sent.ShouldBeEmpty();
    }

    /// <summary>A fatal error shows the message and no retry control.</summary>
    [Fact]
    public async Task AFatalErrorShowsNoRetry()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;

        session.Session.ApplyEvent(PanelEvents.Error(1, "Model unavailable", retryable: false));

        await cut.WaitForAssertionAsync(() => cut.Find("[role='alert'] .ompos-error-message").TextContent.ShouldBe("Model unavailable")).ConfigureAwait(true);
        cut.FindAll("button[aria-label='Retry failed operation']").ShouldBeEmpty();
    }

    /// <summary>A degraded stage shows a polite notice with Dismiss, and dismissing removes it.</summary>
    [Fact]
    public async Task ADegradedStageShowsADismissibleNotice()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;

        session.ApplyStageEvent(PanelEvents.Stage(StageSource.Swarm, "wave", StageStatus.Degraded, "slow"));

        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-panel-notice[data-kind='degraded']").TextContent.ShouldContain(AgentPanelCopy.DegradedNotice(1), Case.Sensitive)).ConfigureAwait(true);
        cut.Find(".ompos-panel-notice[data-kind='degraded']").GetAttribute("role").ShouldBe("status");
        await cut.Find(".ompos-panel-notice button").ClickAsync().ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.FindAll(".ompos-panel-notice").ShouldBeEmpty()).ConfigureAwait(true);
    }

    /// <summary>A lost agent says so and offers Resume, and Resume reaches the host.</summary>
    [Fact]
    public async Task ALostAgentOffersResume()
    {
        var (harness, session, cut) = await OpenAsync(SessionLifecycle.Lost).ConfigureAwait(true);
        using var owned = harness;
        cut.Find(".ompos-panel-notice[data-kind='lost']").TextContent.ShouldContain(AgentPanelCopy.LostHeading, Case.Sensitive);

        await cut.FindAll("button").Single(static button => string.Equals(button.TextContent, AgentPanelCopy.ResumeAgent, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);

        harness.Host.Resumes.ShouldBe(1);
        session.NoticeKind.ShouldNotBe(PanelNoticeKind.None);
    }

    /// <summary>A closed agent hides the composer, explains why and offers Reopen.</summary>
    [Fact]
    public async Task AClosedAgentHidesTheComposerAndOffersReopen()
    {
        var (harness, _, cut) = await OpenAsync(SessionLifecycle.Paused).ConfigureAwait(true);
        using var owned = harness;
        cut.Find("textarea[aria-label='Message']").ShouldNotBeNull();

        await harness.Panel.CloseSessionAsync("a", Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-closed-note").TextContent.ShouldContain(AgentPanelCopy.ClosedHeading, Case.Sensitive)).ConfigureAwait(true);
        cut.FindAll("textarea").ShouldBeEmpty();
        await cut.FindAll("button").Single(static button => string.Equals(button.TextContent, AgentPanelCopy.ReopenAgent, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);
        harness.Host.Resumes.ShouldBe(1);
    }

    /// <summary>Closing a running agent from the header asks first.</summary>
    [Fact]
    public async Task ClosingARunningAgentFromTheHeaderAsksFirst()
    {
        var (harness, session, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;
        session.Session.ApplyEvent(PanelEvents.Text(1, "Working"));

        await cut.Find("button[aria-label='Close this agent']").ClickAsync().ConfigureAwait(true);
        cut.Find(".ompos-confirm-bar[data-kind='close']").TextContent.ShouldContain(session.CloseConfirmationText, Case.Sensitive);
        harness.Host.Closed.ShouldBeEmpty();

        await cut.Find(".ompos-confirm-bar .ompos-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => harness.Host.Closed.ShouldBe(["a"])).ConfigureAwait(true);
    }

    /// <summary>A pending tool approval appears inside the thread with its buttons.</summary>
    [Fact]
    public async Task PendingApprovalsAppearInTheThread()
    {
        var (harness, session, cut) = await OpenAsync(bridge: new PanelBridge()).ConfigureAwait(true);
        using var owned = harness;

        session.PendingApprovals.Add(new PendingToolApproval("a", "req-1", "run_shell", "ls"));

        await cut.WaitForAssertionAsync(() => cut.Find("button[aria-label='Allow once: run_shell']").ShouldNotBeNull()).ConfigureAwait(true);
    }

    /// <summary>The Changes bar shows under the transcript when the session changed a file.</summary>
    [Fact]
    public async Task TheChangesBarShowsUnderTheTranscript()
    {
        using var changes = new ChangeSetModel(new MemoryWorkspace().With("src/a.txt", "new\n"));
        changes.Update([new OmpOS.Contracts.Ui.FileEditBlock { Id = "e-1", CallId = "c-1", Path = "src/a.txt", OldText = "old\n", NewText = "new\n" }]);
        var (harness, _, cut) = await OpenAsync(configure: parameters => parameters.Add(p => p.Changes, changes)).ConfigureAwait(true);
        using var owned = harness;

        cut.Find("section.ompos-changes .ompos-change-path").TextContent.ShouldBe("src/a.txt");
    }

    /// <summary>The thread keeps the skip-link target, the named composer, no script, no inline style, and every control named.</summary>
    [Fact]
    public async Task TheThreadKeepsItsLandmarksAndNamedControls()
    {
        var (harness, _, cut) = await OpenAsync().ConfigureAwait(true);
        using var owned = harness;

        var target = cut.Find("#ompos-conversation");
        target.LocalName.ShouldBe("section");
        target.GetAttribute("tabindex").ShouldBe("-1");
        cut.Find(".ompos-composer-dock textarea").GetAttribute("aria-label").ShouldBe("Message");
        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("[style]").ShouldBeEmpty();
        foreach (var button in cut.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).Trim().ShouldNotBeEmpty();
        }
    }
}
