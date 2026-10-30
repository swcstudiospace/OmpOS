using Autonogrammer.Contracts.Ui;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Tests.Workspace;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Send;

/// <summary>
/// Steering and queued messages over the composed engine. The peer holds each prompt until the test finishes it, so the turn in flight
/// stays open while the test sends messages to the active run. Every test checks what reached the peer, in order.
/// </summary>
public sealed class EngineRunInputSendTests
{
    /// <summary>A peer that holds each prompt until the test finishes the turn, and the transport factory of an engine that talks to it.</summary>
    private sealed class HeldPeer
    {
        /// <summary>Creates the peer.</summary>
        public HeldPeer()
        {
            Agent = new ScriptedAgent(holdPrompt: true);
            Transports = _ => Transport = new ScriptedAgentTransport(Agent.Respond);
        }

        /// <summary>Gets the scripted peer.</summary>
        public ScriptedAgent Agent { get; }

        /// <summary>Gets the transport factory of the engine that talks to the peer.</summary>
        public Func<AcpClientOptions, IAcpTransport> Transports { get; }

        /// <summary>Gets the transport the engine opened, once its first prompt has been sent.</summary>
        public ScriptedAgentTransport? Transport { get; private set; }

        /// <summary>Ends the turn the peer holds, as the peer would when its turn finishes on its own.</summary>
        public void FinishTurn() => Transport.ShouldNotBeNull().Push(Agent.FinishHeldPrompt());
    }

    /// <summary>Types a message into the prompt and presses the send control: it steers the active run, or queues behind a follow-up.</summary>
    private static async Task TypeAndSendWhileActiveAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        RunControlsHarness.Click(ShellHarness.Send(window));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }

    /// <summary>Types a message into the prompt and presses the follow-up control.</summary>
    private static async Task TypeAndQueueFollowUpAsync(MainWindow window, string text)
    {
        ShellHarness.Prompt(window).Text = text;
        RunControlsHarness.Click(RunControlsHarness.FollowUp(window));
        await ShellHarness.DrainAsync(window).ConfigureAwait(true);
    }

    /// <summary>A steer sent during a turn leaves that turn running, and it reaches the peer as a new prompt once the turn ends.</summary>
    [Fact]
    public async Task ASteerDuringATurnDoesNotCancelItAndIsSentAfterTheTurnEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new HeldPeer();

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, peer.Transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            await TypeAndSendWhileActiveAsync(window, "steer").ConfigureAwait(true);

            shell.Runs.IsRunActive.ShouldBeTrue("the steer does not end the run");
            shell.Composer.IsStreaming.ShouldBeTrue("the turn is still in flight");
            peer.Agent.Methods.ShouldNotContain("session/cancel", StringComparer.Ordinal, "a steer never cancels the turn");
            peer.Agent.PromptTexts.ShouldHaveSingleItem().ShouldBe("first");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 2, "the steer as a new prompt", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts.ShouldBe(["first", "steer"]);

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => !shell.Composer.IsStreaming, "the steered turn ending", ct).ConfigureAwait(true);
            shell.Runs.IsRunActive.ShouldBeFalse();
            shell.Runs.QueuedFollowUpCount.ShouldBe(0);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A steer followed by a follow-up reaches the peer in the order they were sent.</summary>
    [Fact]
    public async Task ASteerThenAFollowUpAreSentInTheOrderTheyWereSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new HeldPeer();

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, peer.Transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            await TypeAndSendWhileActiveAsync(window, "steer").ConfigureAwait(true);
            await TypeAndQueueFollowUpAsync(window, "follow-up").ConfigureAwait(true);
            peer.Agent.PromptTexts.ShouldHaveSingleItem("neither message starts while the turn runs");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 2, "the steer", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts[1].ShouldBe("steer");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 3, "the follow-up", ct).ConfigureAwait(true);

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => !shell.Composer.IsStreaming, "the last turn ending", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts.ShouldBe(["first", "steer", "follow-up"]);
            shell.Session.Tree.Blocks.OfType<UserMessageBlock>().Select(block => block.Text).ShouldBe(["first", "steer", "follow-up"]);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A message sent after a follow-up is queued waits behind that follow-up, so the messages keep the order they were sent.</summary>
    [Fact]
    public async Task AMessageSentAfterAQueuedFollowUpWaitsBehindIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new HeldPeer();

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, peer.Transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            await TypeAndQueueFollowUpAsync(window, "follow-up").ConfigureAwait(true);
            await TypeAndSendWhileActiveAsync(window, "later").ConfigureAwait(true);
            shell.Runs.QueuedFollowUpCount.ShouldBe(2, "the send queues behind the follow-up instead of steering");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 2, "the follow-up", ct).ConfigureAwait(true);
            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 3, "the later message", ct).ConfigureAwait(true);
            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => !shell.Composer.IsStreaming, "the last turn ending", ct).ConfigureAwait(true);

            peer.Agent.PromptTexts.ShouldBe(["first", "follow-up", "later"]);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Two steers accepted during one turn are counted until each steer starts its own turn. Finishing the first turn starts the first
    /// steer turn, so one steer is still held; finishing the second starts the second steer turn, so none is held.
    /// </summary>
    [Fact]
    public async Task TwoHeldSteersAreCountedUntilEachStartsItsTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new HeldPeer();

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, peer.Transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            await TypeAndSendWhileActiveAsync(window, "steer one").ConfigureAwait(true);
            await TypeAndSendWhileActiveAsync(window, "steer two").ConfigureAwait(true);
            shell.Runs.SteersHeld.ShouldBe(2, "both steers wait for the first turn to end");
            RunControlsHarness.PendingText(window).ShouldContain("2 steering messages are held");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 2, "the first steer turn", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts[1].ShouldBe("steer one");
            shell.Runs.SteersHeld.ShouldBe(1, "the second steer still waits for its own turn");
            RunControlsHarness.PendingText(window).ShouldContain("1 steering message is held");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 3, "the second steer turn", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts[2].ShouldBe("steer two");
            shell.Runs.SteersHeld.ShouldBe(0, "no steer waits once the second steer turn started");
            RunControlsHarness.PendingText(window).ShouldNotContain("steering message");

            peer.FinishTurn();
            await RunControlsHarness.UntilAsync(() => !shell.Composer.IsStreaming, "the last turn ending", ct).ConfigureAwait(true);
            peer.Agent.PromptTexts.ShouldBe(["first", "steer one", "steer two"]);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>
    /// Stop cancels the turn in flight. Messages still held behind it are not sent, the queue is empty afterwards, and the transcript
    /// says how many were not sent.
    /// </summary>
    [Fact]
    public async Task StopSendsNothingFurtherAndSaysHowManyMessagesWereNotSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var peer = new HeldPeer();

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workspace = new TempWorkspace();
            var shell = await SendShell.ShowAsync(workspace.Root, peer.Transports, [], ct).ConfigureAwait(true);
            await using var shellScope = shell.ConfigureAwait(true);
            var window = shell.Window;

            await shell.SendAsync("first").ConfigureAwait(true);
            await RunControlsHarness.UntilAsync(() => peer.Agent.PromptTexts.Count == 1, "the first prompt", ct).ConfigureAwait(true);

            await TypeAndSendWhileActiveAsync(window, "steer").ConfigureAwait(true);
            await TypeAndQueueFollowUpAsync(window, "follow-up").ConfigureAwait(true);

            RunControlsHarness.Click(ShellHarness.Stop(window));
            await RunControlsHarness.UntilAsync(() => !shell.Composer.IsStreaming, "the stopped turn ending", ct).ConfigureAwait(true);

            peer.Agent.Methods.ShouldContain("session/cancel", StringComparer.Ordinal);
            peer.Agent.PromptTexts.ShouldHaveSingleItem("nothing held after Stop is sent").ShouldBe("first");
            shell.Runs.IsRunActive.ShouldBeFalse();
            shell.Runs.QueuedFollowUpCount.ShouldBe(0);
            shell.Session.Tree.Blocks.OfType<MarkdownBlock>().Select(block => block.Markdown)
                .ShouldContain("2 queued messages were not sent.", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }
}
