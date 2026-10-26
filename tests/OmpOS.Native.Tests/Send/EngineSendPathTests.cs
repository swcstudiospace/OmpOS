using System.Text;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Native.Tests.RunControls;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Send;

/// <summary>
/// The production send path and the approval path over the composed engine. Each test drives the main window through a scripted
/// <c>omp acp</c> peer, so no live omp process runs. The session is the production head's own: its send path, approval bridge and
/// engine are the ones the application builds for a session.
/// </summary>
public sealed class EngineSendPathTests
{
    private const string RequestId = "request-1";

    /// <summary>The transport factory of a head whose engines talk to the scripted agent.</summary>
    private static Func<AcpClientOptions, IAcpTransport> Over(ScriptedAgent agent) => _ => new ScriptedAgentTransport(agent.Respond);

    /// <summary>Launch starts no engine. The first send reaches the peer, and the engine's transport is opened once for it.</summary>
    [Fact]
    public async Task TheFirstSendStartsTheEngineAndLaunchStartsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent();
        var started = 0;
        Func<AcpClientOptions, IAcpTransport> transports = _ =>
        {
            started++;
            return new ScriptedAgentTransport(agent.Respond);
        };

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            started.ShouldBe(0, "launch starts no engine");
            agent.Methods.ShouldBeEmpty("nothing reached the peer at launch");
            agent.PromptTexts.ShouldBeEmpty();

            await shell.SendAsync("hello").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

            started.ShouldBe(1, "the first send starts the engine once");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// The production policy admits a parked request of an admitted kind: an approve settles it as allowed and the engine's host
    /// receives the grant. The head composes the session's engine itself, so the test asks the session for it.
    /// </summary>
    [Fact]
    public async Task TheProductionPolicySettlesAParkedAdmittedRequestAsAllowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var transports = Over(new ScriptedAgent());

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, Program.ProductionAllowedKinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            var engine = await shell.EngineAsync(ct).ConfigureAwait(true);
            var parked = engine.Resolver.ResolveAsync(new PermissionRequestEvent("call-1", "edit", "{}", RequestId), ct);
            var approval = shell.AddRequest(RequestId);
            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the allowance", ct).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Allowed);
            (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Allow);
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
        var transports = Over(agent);
        string[] kinds = [.. Program.ProductionAllowedKinds];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("fetch the page").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

            shell.RunControls.Approvals.ShouldBeEmpty("a forbidden kind never becomes an approval");
            agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("reject");
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
        var transports = Over(agent);
        string[] kinds = [.. Program.ProductionAllowedKinds];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("edit the file").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
            var approval = shell.RunControls.Approvals.Single();

            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Allowed, "the allowance", ct).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

            agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("allow");
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
        var transports = Over(agent);
        string[] kinds = [.. Program.ProductionAllowedKinds];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("read the file").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
            var approval = shell.RunControls.Approvals.Single();

            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Allowed, "the allowance", ct).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

            agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("allow");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A submitted prompt reaches the composed host, and its streamed reply lands in the transcript.</summary>
    [Fact]
    public async Task ASendSubmitsThePromptThroughTheComposedHostAndStreamsTheReply()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(reply: "Hi from omp");
        var transports = Over(agent);
        string[] kinds = [];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("hello").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

            agent.PromptTexts.ShouldHaveSingleItem().ShouldBe("hello");
            shell.Composer.Draft.ShouldBeEmpty("the host accepted the send, so the draft clears");
            shell.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown).ShouldHaveSingleItem().ShouldBe("Hi from omp");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A sent prompt is echoed into the transcript as the user's own turn, ahead of the reply it prompted.</summary>
    [Fact]
    public async Task ASentPromptAppearsInTheTranscriptAsAUserMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(reply: "Hi from omp");
        var transports = Over(agent);
        string[] kinds = [];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("hello").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the reply", ct).ConfigureAwait(true);

            shell.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldHaveSingleItem().ShouldBe("hello");
            shell.Session.Tree.Blocks[0].ShouldBeOfType<UserMessageBlock>("the user's turn comes before the reply");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A permission request the host parks becomes a pending row in the approval UI.</summary>
    [Fact]
    public async Task AParkedHostPermissionRequestAppearsInTheApprovalUi()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true);
        var transports = Over(agent);
        string[] kinds = ["bash"];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("run ls").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);

            var approval = shell.RunControls.Approvals.Single();
            approval.ToolName.ShouldBe("bash");
            approval.RequestId.ShouldBe(ScriptedAgent.PermissionRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            approval.Status.ShouldBe(ToolApprovalStatus.Pending);
            agent.PermissionOptionIds.ShouldBeEmpty("nothing is decided until the user clicks");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Deny settles the parked host request as denied, and the agent receives the reject option.</summary>
    [Fact]
    public async Task DenyingAParkedHostPermissionRequestSettlesItDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(requestPermission: true);
        var transports = Over(agent);
        string[] kinds = ["bash"];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("run ls").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.RunControls.Approvals.Count == 1, "the approval row", ct).ConfigureAwait(true);
            var approval = shell.RunControls.Approvals.Single();

            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Denied, "the denial", ct).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the turn ending", ct).ConfigureAwait(true);

            agent.PermissionOptionIds.ShouldHaveSingleItem().ShouldBe("reject");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Under the deny-all policy an approve reports Refused and leaves the parked request pending, so a later deny still settles it.
    /// </summary>
    [Fact]
    public async Task ApproveUnderDenyAllReportsRefusedAndLeavesTheParkedRequestPending()
    {
        var ct = TestContext.Current.CancellationToken;
        var transports = Over(new ScriptedAgent());

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            var engine = await shell.EngineAsync(ct).ConfigureAwait(true);
            var parked = engine.Resolver.ResolveAsync(new PermissionRequestEvent("call-1", "bash", "{}", RequestId), ct);
            var approval = shell.AddRequest(RequestId);

            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Approve tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status != ToolApprovalStatus.Pending, "the refusal", ct).ConfigureAwait(true);

            approval.Status.ShouldBe(ToolApprovalStatus.Refused);
            parked.IsCompleted.ShouldBeFalse("a refused approve leaves the request parked");

            await RunControlsHarness.UntilAsync(() => RunControlsHarness.Named(shell.Window, "Deny tool request").IsEnabled, "deny to be enabled again", ct).ConfigureAwait(true);
            RunControlsHarness.Click(RunControlsHarness.Named(shell.Window, "Deny tool request"));
            await RunControlsHarness.UntilAsync(() => approval.Status == ToolApprovalStatus.Denied, "the denial", ct).ConfigureAwait(true);

            (await parked.ConfigureAwait(true)).ShouldBe(PermissionDecision.Deny);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A send the host cannot start is shown inline in the transcript with its reason, and the composer is free again.</summary>
    [Fact]
    public async Task ASendThatFailsShowsItsReasonInlineInTheTranscript()
    {
        var ct = TestContext.Current.CancellationToken;
        Func<AcpClientOptions, IAcpTransport> transports = _ => throw new InvalidOperationException("omp executable was not found");

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("hello").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.RecoverableError, "the failure", ct).ConfigureAwait(true);

            shell.Session.LastError.ShouldNotBeNull().ShouldContain("omp executable was not found");
            shell.Session.Tree.Blocks.OfType<ErrorBlock>().ShouldHaveSingleItem().Message.ShouldContain("omp executable was not found");
            shell.Composer.IsStreaming.ShouldBeFalse();
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
        Func<AcpClientOptions, IAcpTransport> transports = _ => new ScriptedAgentTransport(frame =>
        {
            if (Encoding.UTF8.GetString(frame).Contains("\"session/prompt\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("the peer broke during the turn");
            }

            return agent.Respond(frame);
        });

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("hello").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.RecoverableError, "the failure", ct).ConfigureAwait(true);

            shell.Session.LastError.ShouldNotBeNull().ShouldContain("the peer broke during the turn");
            shell.Session.Tree.Blocks.OfType<ErrorBlock>().ShouldHaveSingleItem().Message.ShouldContain("the peer broke during the turn");
            shell.Composer.IsStreaming.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Stop cancels the turn in flight through the composed host, and the turn ends with the agent's stop reason.</summary>
    [Fact]
    public async Task StopCancelsTheInFlightTurnThroughTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = new ScriptedAgent(holdPrompt: true);
        var transports = Over(agent);
        string[] kinds = [];

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, kinds, ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);

            await shell.SendAsync("long task").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the prompt", ct).ConfigureAwait(true);
            shell.Composer.IsStreaming.ShouldBeTrue();

            await shell.Composer.StopAsync().ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => shell.Session.State == SessionViewState.Success, "the cancelled turn", ct).ConfigureAwait(true);

            agent.Methods.Count(method => string.Equals(method, "session/cancel", StringComparison.Ordinal)).ShouldBe(1);
            shell.Composer.IsStreaming.ShouldBeFalse();
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
        Func<AcpClientOptions, IAcpTransport> transports = _ => transport = new ScriptedAgentTransport(agent.Respond);

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            ShellHarness.Prompt(window).Text = "steer";
            RunControlsHarness.Click(ShellHarness.Send(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            ShellHarness.Prompt(window).Text = "follow-up";
            RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
            await ShellHarness.DrainAsync(window).ConfigureAwait(true);
            shell.Runs.QueuedFollowUpCount.ShouldBe(1, "the follow-up waits in the scheduler");

            // The peer closes its stream while the held turn is in flight, so the turn faults.
            await transport.ShouldNotBeNull().CloseAsync(CancellationToken.None).ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(
                () => shell.Session.Tree.Blocks.OfType<MarkdownBlock>().Any(block => string.Equals(block.Markdown, "2 queued messages were not sent.", StringComparison.Ordinal)),
                "the not-sent line",
                ct).ConfigureAwait(true);

            var blocks = shell.Session.Tree.Blocks.ToList();
            blocks.Count(block => block is ErrorBlock).ShouldBe(1, "one row per fault");
            var fault = blocks.FindIndex(block => block is ErrorBlock);
            var notSent = blocks.FindIndex(block => block is MarkdownBlock { Markdown: "2 queued messages were not sent." });
            notSent.ShouldBeGreaterThan(fault, "the not-sent line follows the fault reason");
            ((ErrorBlock)blocks[fault]).Message.ShouldContain("closed");

            agent.PromptTexts.ShouldHaveSingleItem("nothing held is sent after the fault").ShouldBe("first");
            shell.Runs.IsRunActive.ShouldBeFalse();
            shell.Composer.IsStreaming.ShouldBeFalse();
            shell.Runs.QueuedFollowUpCount.ShouldBe(0);
        }, ct).ConfigureAwait(true);
    }
}
