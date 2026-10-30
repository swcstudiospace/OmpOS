using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The Agent Panel of the production head over scripted omp peers: it opens Loading and then Empty without any plugin, a New agent
/// session sends through its own engine and reaches the registry, a session that was running when the application ended is shown lost
/// and resumes, and closing a session from the panel stops its engine and offers Reopen.
/// </summary>
public sealed class AgentHeadPanelTests
{
    /// <summary>The panel is Loading from the first frame until the registry is loaded, then Empty: a missing plugins root does not stop it.</summary>
    [Fact]
    public async Task ThePanelOpensLoadingThenEmptyEvenWithoutAPluginsRoot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        head.Panel.State.ShouldBe(PanelState.Loading);

        await head.InitializeAsync(ct).ConfigureAwait(true);

        head.Panel.State.ShouldBe(PanelState.Empty);
        head.Panel.SessionCount.ShouldBe(0);
        head.Panel.BannerText.ShouldBeNull();
    }

    /// <summary>A New agent session sends through its own engine, its turn is mirrored into the registry, and its row ends Completed.</summary>
    [Fact]
    public async Task ANewAgentSendsThroughItsEngineAndTheRegistryFollowsTheTurn()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        var omp = new ScriptedOmp();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);

                await head.Panel.NewSessionAsync(ct).ConfigureAwait(true);

                head.Panel.State.ShouldBe(PanelState.Ready);
                var selected = head.Panel.Selected.ShouldNotBeNull();
                var runtime = AgentSessionRuntime.For(selected).ShouldNotBeNull();
                runtime.Composer.Draft = "hello from the panel";
                await runtime.Composer.SendAsync().ConfigureAwait(true);
                await RunControlsHarness.UntilAsync(
                    () => head.Host.Registry!.TryGet(selected.Id, out var record) && record!.Lifecycle == SessionLifecycle.Completed,
                    "the completed turn in the registry",
                    ct).ConfigureAwait(true);

                omp.Requests("session/prompt").ShouldHaveSingleItem()["params"]!["prompt"]![0]!["text"]!.GetValue<string>().ShouldBe("hello from the panel");
                head.Host.Registry!.TryGet(selected.Id, out var stored).ShouldBeTrue();
                stored!.AcpSessionId.ShouldBe("native-1");
                head.Catalog.Sessions.Single().Status.ShouldBe(AgentRowStatus.Completed);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A session whose turn was running when the application ended is shown Lost, and Resume attaches an engine again and shows the Resumed notice.</summary>
    [Fact]
    public async Task ALostSessionResumesFromThePanel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var id = await LeaveARunningTurnAsync(tree, ct).ConfigureAwait(true);
        var omp = new ScriptedOmp();
        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);

                head.Panel.SessionCount.ShouldBe(1);
                head.Catalog.Sessions.Single().Status.ShouldBe(AgentRowStatus.Lost);
                head.Panel.SelectSession(id).ShouldBeTrue();
                var selected = head.Panel.Selected.ShouldNotBeNull();
                selected.IsLost.ShouldBeTrue();
                selected.ResumeCommand.CanExecute(null).ShouldBeTrue();

                await selected.ResumeCommand.ExecuteAsync(null).ConfigureAwait(true);

                selected.NoticeKind.ShouldBe(PanelNoticeKind.Resumed);
                head.Host.Registry!.TryGet(id, out var resumed).ShouldBeTrue();
                resumed!.Lifecycle.ShouldBe(SessionLifecycle.Created);
                omp.Launches.ShouldBeEmpty("resuming attaches an engine that starts with the next prompt");
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Closing from the panel stops the engine and shows the row Closed; Reopen attaches an engine again and clears the closed state.</summary>
    [Fact]
    public async Task ClosingFromThePanelStopsTheEngineAndReopenAttachesAnotherOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = AgentHead.Create(Options(tree, omp));
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                await head.Panel.NewSessionAsync(ct).ConfigureAwait(true);
                var selected = head.Panel.Selected.ShouldNotBeNull();
                var slot = head.Host.SlotOf(selected.Id).ShouldNotBeNull();
                await SendAsync(slot, "hello", ct).ConfigureAwait(true);
                omp.ClosedTransports.ShouldBe(0);

                await head.Panel.CloseSessionAsync(selected.Id, ct).ConfigureAwait(true);

                omp.ClosedTransports.ShouldBe(1);
                selected.IsClosed.ShouldBeTrue();
                head.Catalog.Sessions.Single().Status.ShouldBe(AgentRowStatus.Closed);
                selected.ReopenCommand.CanExecute(null).ShouldBeTrue();

                await selected.ReopenCommand.ExecuteAsync(null).ConfigureAwait(true);

                selected.IsClosed.ShouldBeFalse();
                head.Host.IsClosed(selected.Id).ShouldBeFalse();
                await SendAsync(slot, "again", ct).ConfigureAwait(true);
                omp.Launches.Count.ShouldBe(2);
                omp.Requests("session/load").ShouldHaveSingleItem()["params"]!["sessionId"]!.GetValue<string>().ShouldBe("native-1");
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Starts a head, runs a turn that stays open, marks the session Running as its runtime would, and disposes the head without ending the turn, as a crash would.</summary>
    private static async Task<string> LeaveARunningTurnAsync(TempTree tree, CancellationToken cancellationToken)
    {
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true };
        var head = AgentHead.Create(Options(tree, omp));
        await using (head.ConfigureAwait(true))
        {
            await head.InitializeAsync(cancellationToken).ConfigureAwait(true);
            var (id, slot) = await NewSlotAsync(head, cancellationToken).ConfigureAwait(true);
            var turn = slot.Prompts.SubmitAsync("long work", cancellationToken).GetAsyncEnumerator(cancellationToken);
            await using (turn.ConfigureAwait(true))
            {
                (await turn.MoveNextAsync().ConfigureAwait(true)).ShouldBeTrue();
                head.Host.OnTurnChanged(slot, new SessionTurnEventArgs(SessionTurnKind.Started));
            }

            return id;
        }
    }
}
