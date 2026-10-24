using System.Text;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Send;

/// <summary>
/// The production send path and the approval path over the composed engine. Each test drives the main window through a scripted
/// <c>omp acp</c> peer, so no live omp process runs.
/// </summary>
public sealed class EngineSendPathTests
{
    private const string RequestId = "request-1";

    /// <summary>The composed engine's host talks to the scripted agent, and its policy admits <paramref name="allowedKinds"/>.</summary>
    private static Lazy<EngineComposition> ComposedOver(ScriptedAgent agent, params string[] allowedKinds) =>
        new(() => EngineComposition.Compose(allowedKinds, _ => new ScriptedAgentTransport(agent.Respond)));

    private static MainWindow ShowOver(TempWorkspace workspace, Lazy<EngineComposition> engine)
    {
        var window = new MainWindow(new App.ComposedApprovalBridge(engine), new EngineRunInput(), MotionPreference.System, workspace.Root, engine);
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static async Task SendAsync(MainWindow window, string text)
    {
        window.Composer.Draft = text;
        await window.Composer.SendAsync().ConfigureAwait(true);
    }

    /// <summary>Launch composes no engine. The first send composes it, and only then does a prompt reach the host.</summary>
    [Fact]
    public async Task TheFirstSendComposesTheEngineAndLaunchComposesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent();
        var composed = 0;
        var engine = new Lazy<EngineComposition>(() =>
        {
            composed++;
            return EngineComposition.Compose([], _ => new ScriptedAgentTransport(agent.Respond));
        });

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                composed.ShouldBe(0, "launch composes no engine");
                engine.IsValueCreated.ShouldBeFalse();
                agent.PromptTexts.ShouldBeEmpty();

                await SendAsync(window, "hello").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

                composed.ShouldBe(1, "the first send composes the engine once");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// The production engine composes on the first decision, and its named-kind policy admits a parked request of an admitted kind:
    /// an approve settles it as allowed and the host receives the grant.
    /// </summary>
    [Fact]
    public async Task TheProductionEngineComposesOnTheFirstDecisionAndSettlesAParkedAdmittedRequestAsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new Lazy<EngineComposition>(Program.ComposeProductionEngine);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                engine.IsValueCreated.ShouldBeFalse("launch composes no engine");

                var parked = engine.Value.Resolver.ResolveAsync(new PermissionRequestEvent("call-1", "edit", "{}", RequestId), ct);
                var approval = RunControlsHarness.AddRequest(window, RequestId);
                RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the allowance", ct).ConfigureAwait(true);

                engine.IsValueCreated.ShouldBeTrue("the first decision composes the engine");
                approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
                (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A permission request whose kind the production policy does not name is denied by the host before it parks: no approval row
    /// appears, and the agent receives the reject option at once.
    /// </summary>
    [Fact]
    public async Task TheProductionPolicyDeniesAKindOutsideTheNamedSetBeforeItParks()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true, permissionKind: "fetch");
        var engine = ComposedOver(agent, Program.ProductionAllowedKinds);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "fetch the page").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

                window.RunControls.Approvals.ShouldBeEmpty("a forbidden kind never becomes an approval");
                agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("reject");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A permission request of an admitted kind raised by the host becomes a pending approval, and an approve grants it: the agent
    /// receives the allow option.
    /// </summary>
    [Fact]
    public async Task AnAdmittedKindRaisedByTheHostAppearsInTheApprovalUiAndApproveGrantsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true, permissionKind: "edit");
        var engine = ComposedOver(agent, Program.ProductionAllowedKinds);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "edit the file").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
                var approval = window.RunControls.Approvals.Single();

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Allowed, "the allowance", ct).ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

                agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("allow");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A permission request of kind <c>read</c> raised by the host under the production composition becomes a pending approval, because
    /// the owner admitted <c>read</c> to the production set. An approve grants it: the agent receives the allow option.
    /// </summary>
    [Fact]
    public async Task AReadRequestRaisedByTheHostAppearsInTheApprovalUi()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true, permissionKind: "read");
        var engine = ComposedOver(agent, Program.ProductionAllowedKinds);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "read the file").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
                var approval = window.RunControls.Approvals.Single();

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Allowed, "the allowance", ct).ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

                agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("allow");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A submitted prompt reaches the composed host, and its streamed reply lands in the transcript.</summary>
    [Fact]
    public async Task ASendSubmitsThePromptThroughTheComposedHostAndStreamsTheReply()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(reply: "Hi from omp");
        var engine = ComposedOver(agent);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "hello").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

                agent.PromptTexts.ShouldHaveSingleItem().ShouldBe("hello");
                window.Composer.Draft.ShouldBeEmpty("the host accepted the send, so the draft clears");
                window.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown).ShouldHaveSingleItem().ShouldBe("Hi from omp");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A sent prompt is echoed into the transcript as the user's own turn, ahead of the reply it prompted.</summary>
    [Fact]
    public async Task ASentPromptAppearsInTheTranscriptAsAUserMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(reply: "Hi from omp");
        var engine = ComposedOver(agent);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "hello").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

                window.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldHaveSingleItem().ShouldBe("hello");
                window.Session.Tree.Blocks[0].ShouldBeOfType<UserMessageBlock>("the user's turn comes before the reply");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A permission request the host parks becomes a pending row in the approval UI.</summary>
    [Fact]
    public async Task AParkedHostPermissionRequestAppearsInTheApprovalUi()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true);
        var engine = ComposedOver(agent, "bash");

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "run ls").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);

                var approval = window.RunControls.Approvals.Single();
                approval.ToolName.ShouldBe("bash");
                approval.RequestId.ShouldBe(ScriptedAgent.PermissionRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                approval.Status.ShouldBe(ToolApprovalStatus.Pending);
                agent.PermissionOptionIds.ShouldBeEmpty("nothing is decided until the user clicks");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Deny settles the parked host request as denied, and the agent receives the reject option.</summary>
    [Fact]
    public async Task DenyingAParkedHostPermissionRequestSettlesItDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true);
        var engine = ComposedOver(agent, "bash");

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "run ls").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
                var approval = window.RunControls.Approvals.Single();

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Denied, "the denial", ct).ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

                agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("reject");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Under the deny-all policy an approve reports Refused and leaves the parked request pending, so a later deny still settles it.
    /// </summary>
    [Fact]
    public async Task ApproveUnderDenyAllReportsRefusedAndLeavesTheParkedRequestPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = ComposedOver(new ScriptedAgent());

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                var parked = engine.Value.Resolver.ResolveAsync(new PermissionRequestEvent("call-1", "bash", "{}", RequestId), ct);
                var approval = RunControlsHarness.AddRequest(window, RequestId);

                RunControlsHarness.Click(RunControlsHarness.Named(window, "Approve tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the refusal", ct).ConfigureAwait(true);

                approval.Status.ShouldBe(ToolApprovalStatus.Refused);
                parked.IsCompleted.ShouldBeFalse("a refused approve leaves the request parked");

                await RunControlsHarness.UntilAsync(() => RunControlsHarness.Named(window, "Deny tool request").IsEnabled, "deny to be enabled again", ct).ConfigureAwait(true);
                RunControlsHarness.Click(RunControlsHarness.Named(window, "Deny tool request"));
                await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Denied, "the denial", ct).ConfigureAwait(true);

                (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A send the host cannot start is shown inline in the transcript with its reason, and the composer is free again.</summary>
    [Fact]
    public async Task ASendThatFailsShowsItsReasonInlineInTheTranscript()
    {
        var ct = TestContext.Current.CancellationToken;
        var engine = new Lazy<EngineComposition>(() =>
            EngineComposition.Compose([], _ => throw new InvalidOperationException("omp executable was not found")));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "hello").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.FatalError, "the failure", ct).ConfigureAwait(true);

                window.Session.LastError.ShouldNotBeNull().ShouldContain("omp executable was not found");
                window.Session.Tree.Blocks.OfType<ErrorBlock>().ShouldHaveSingleItem().Message.ShouldContain("omp executable was not found");
                window.Composer.IsStreaming.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A plain exception the peer raises while a turn runs, which no typed host failure describes, is shown inline with its message
    /// and is not lost to an unobserved fault.
    /// </summary>
    [Fact]
    public async Task AnUnexpectedHostFailureIsShownInlineNotLost()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent();
        var engine = new Lazy<EngineComposition>(() => EngineComposition.Compose([], _ => new ScriptedAgentTransport(frame =>
        {
            if (Encoding.UTF8.GetString(frame).Contains("\"session/prompt\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("the peer broke during the turn");
            }

            return agent.Respond(frame);
        })));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "hello").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.FatalError, "the failure", ct).ConfigureAwait(true);

                window.Session.LastError.ShouldNotBeNull().ShouldContain("the peer broke during the turn");
                window.Session.Tree.Blocks.OfType<ErrorBlock>().ShouldHaveSingleItem().Message.ShouldContain("the peer broke during the turn");
                window.Composer.IsStreaming.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Stop cancels the turn in flight through the composed host, and the turn ends with the agent's stop reason.</summary>
    [Fact]
    public async Task StopCancelsTheInFlightTurnThroughTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        var engine = ComposedOver(agent);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "long task").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the prompt", ct).ConfigureAwait(true);
                window.Composer.IsStreaming.ShouldBeTrue();

                await window.Composer.StopAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => window.Session.State == SessionViewState.Success, "the cancelled turn", ct).ConfigureAwait(true);

                agent.Methods.Count(method => string.Equals(method, "session/cancel", StringComparison.Ordinal)).ShouldBe(1);
                window.Composer.IsStreaming.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// A turn that faults while a steer and a follow-up are held shows the fault reason first, then how many held messages were not
    /// sent. Nothing held is sent after the fault, and the run and the composer end.
    /// </summary>
    [Fact]
    public async Task AFaultWithHeldMessagesShowsTheReasonThenHowManyWereNotSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        ScriptedAgentTransport? transport = null;
        var engine = new Lazy<EngineComposition>(() => EngineComposition.Compose([], _ => transport = new ScriptedAgentTransport(agent.Respond)));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var window = ShowOver(workspace, engine);
            try
            {
                await SendAsync(window, "first").ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

                ShellHarness.Prompt(window).Text = "steer";
                RunControlsHarness.Click(ShellHarness.Send(window));
                await ShellHarness.DrainAsync(window).ConfigureAwait(true);
                ShellHarness.Prompt(window).Text = "follow-up";
                RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
                await ShellHarness.DrainAsync(window).ConfigureAwait(true);
                window.Runs.QueuedFollowUpCount.ShouldBe(1, "the follow-up waits in the scheduler");

                // The peer closes its stream while the held turn is in flight, so the turn faults.
                await transport.ShouldNotBeNull().CloseAsync(CancellationToken.None).ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(
                    () => window.Session.Tree.Blocks.OfType<MarkdownBlock>().Any(block => string.Equals(block.Markdown, "2 queued messages were not sent.", StringComparison.Ordinal)),
                    "the not-sent line",
                    ct).ConfigureAwait(true);

                var blocks = window.Session.Tree.Blocks.ToList();
                blocks.Count(block => block is ErrorBlock).ShouldBe(1, "one row per fault");
                var fault = blocks.FindIndex(block => block is ErrorBlock);
                var notSent = blocks.FindIndex(block => block is MarkdownBlock { Markdown: "2 queued messages were not sent." });
                notSent.ShouldBeGreaterThan(fault, "the not-sent line follows the fault reason");
                ((ErrorBlock)blocks[fault]).Message.ShouldContain("closed");

                agent.PromptTexts.ShouldHaveSingleItem("nothing held is sent after the fault").ShouldBe("first");
                window.Runs.IsRunActive.ShouldBeFalse();
                window.Composer.IsStreaming.ShouldBeFalse();
                window.Runs.QueuedFollowUpCount.ShouldBe(0);
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }
}
