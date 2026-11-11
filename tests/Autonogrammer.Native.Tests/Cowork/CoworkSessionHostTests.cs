#pragma warning disable CA2007

using System.Net;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia.Threading;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The Cowork host over the phase-69 registry. No live omp is started.</summary>
public sealed class CoworkSessionHostTests
{
    [Fact]
    public async Task CreateOpenCloseAndReloadRecoverOnce()
    {
        using var root = new TempDir();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        await using var host = Host(root.Path, omp);
        var id = await host.CreateSessionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.Recoveries.ShouldBe(1);
        var view = await host.OpenSessionAsync(Row(id), TestContext.Current.CancellationToken).ConfigureAwait(true);
        view.Id.ShouldBe(id);
        await host.CloseSessionAsync(id, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await host.ReloadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.Recoveries.ShouldBe(1);
        await host.CreateSessionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.Recoveries.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTerminalSessionAsyncThrows()
    {
        using var root = new TempDir();
        await using var host = Host(root.Path, new ScriptedOmp());
        await Should.ThrowAsync<NotSupportedException>(() => host.CreateTerminalSessionAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task TurnOriginSetPerPromptAndModelOriginIgnored()
    {
        using var root = new TempDir();
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true });
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                using var arguments = JsonDocument.Parse("{\"origin\":\"Dispatch\",\"text\":\"hi\"}");
                await host.SendAsync(id, "hello", arguments.RootElement, ct).ConfigureAwait(true);
                await UntilRunningAsync(view, ct).ConfigureAwait(true);
                await Should.ThrowAsync<InvalidOperationException>(() => host.RetryAsync(id, arguments.RootElement, ct)).ConfigureAwait(true);
                await host.SteerAsync(id, "left", arguments.RootElement, ct).ConfigureAwait(true);
                host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local, CoworkTurnOrigin.Local]);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }
    [Fact]
    public async Task ARetryWhileATurnRunsIsRefusedAndSendsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                await host.SendAsync(id, "first", null, ct).ConfigureAwait(true);
                await UntilRunningAsync(view, ct).ConfigureAwait(true);
                (await RequestTextAsync(omp, "session/prompt", ct).ConfigureAwait(true)).ShouldBe("first");

                await Should.ThrowAsync<InvalidOperationException>(() => host.RetryAsync(id, null, ct)).ConfigureAwait(true);

                omp.Requests("_session/steering").ShouldBeEmpty();
                omp.Requests("session/prompt").Count.ShouldBe(1);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task SteerWhileRunActive_InjectsTextAndSetsLocalOrigin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                await host.SendAsync(id, "first", null, ct).ConfigureAwait(true);
                await UntilRunningAsync(view, ct).ConfigureAwait(true);
                (await RequestTextAsync(omp, "session/prompt", ct).ConfigureAwait(true)).ShouldBe("first");

                await host.SteerAsync(id, "steer text here", null, ct).ConfigureAwait(true);
                (await RequestTextAsync(omp, "_session/steering", ct).ConfigureAwait(true)).ShouldBe("steer text here");
                host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local, CoworkTurnOrigin.Local]);
                // text reaches the LiveSteering, Injected path taken
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task SteerWhenNotRunning_HandsToPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { AdvertiseAuthentication = true, SteeringSupported = true, SteerOutcome = "promptRequired" };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                await host.SteerAsync(id, "steer when idle", null, ct).ConfigureAwait(true);
                (await RequestTextAsync(omp, "session/prompt", ct).ConfigureAwait(true)).ShouldBe("steer when idle");
                host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local]);
                // hands to prompt path
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }


    [Fact]
    public async Task ASentPromptReachesTheEngineAsAPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

                await host.SendAsync(id, "hello cowork", null, ct).ConfigureAwait(true);

                (await RequestTextAsync(omp, "session/prompt", ct).ConfigureAwait(true)).ShouldBe("hello cowork");
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task ASendDuringARunIsDeliveredIntoThatRunAsASteer()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                await host.SendAsync(id, "first", null, ct).ConfigureAwait(true);
                await UntilRunningAsync(view, ct).ConfigureAwait(true);
                (await RequestTextAsync(omp, "session/prompt", ct).ConfigureAwait(true)).ShouldBe("first");

                await host.SendAsync(id, "second", null, ct).ConfigureAwait(true);

                (await RequestTextAsync(omp, "_session/steering", ct).ConfigureAwait(true)).ShouldBe("second");
                omp.Requests("session/prompt").Count.ShouldBe(1);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task ASendFromAPoolThreadRaisesItsRunChangesOnTheUiThread()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        var omp = new ScriptedOmp { HoldAfterFirstChunk = true, AdvertiseAuthentication = true, SteeringSupported = true, SteerOutcome = "promptRequired" };
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, omp);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
                var runs = AgentSessionRuntime.For(view).ShouldNotBeNull().Runs;
                var changes = 0;
                var changesOffTheUiThread = 0;
                runs.PropertyChanged += (_, _) =>
                {
                    changes++;
                    if (!Dispatcher.UIThread.CheckAccess())
                    {
                        changesOffTheUiThread++;
                    }
                };

                await Task.Run(() => host.SendAsync(id, "first", null, ct), ct).ConfigureAwait(true);
                await UntilRunningAsync(view, ct).ConfigureAwait(true);
                await Task.Run(() => host.SendAsync(id, "second", null, ct), ct).ConfigureAwait(true);

                changes.ShouldBeGreaterThan(0);
                changesOffTheUiThread.ShouldBe(0);
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    private static async Task UntilRunningAsync(AgentSessionViewModel view, CancellationToken cancellationToken)
    {
        var runtime = AgentSessionRuntime.For(view).ShouldNotBeNull();
        for (var attempt = 0; attempt < 500 && !runtime.Runs.IsRunActive; attempt++)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }

        runtime.Runs.IsRunActive.ShouldBeTrue("the first turn should be running");
    }

    private static async Task<string> RequestTextAsync(ScriptedOmp omp, string method, CancellationToken cancellationToken, int index = 0)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var requests = omp.Requests(method);
            if (requests.Count > index)
            {
                return requests[index]["params"]!["prompt"]![0]!["text"]!.GetValue<string>();
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException($"The fake omp never received {method} request {index + 1}.");
    }

    [Fact]
    public void ImagesTheModelCannotReadAreRefusedAndOverLimitImagesAreUnsent()
    {
        var png = new AcpPromptImage("shot.png", "image/png", new byte[] { 1, 2, 3 });
        CoworkSessionHost.AttachImages(false, [png]).Reason.ShouldBe(Autonogrammer.Presentation.Composer.Attachments.AttachmentCopy.ModelRefuses);
        var huge = new AcpPromptImage("big.png", "image/png", new byte[11 * 1024 * 1024]);
        var over = CoworkSessionHost.AttachImages(true, [huge]);
        over.Sent.ShouldBeFalse();
        over.ChipText.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenStartsAHealthyRealGatewayAndCloseRemovesOnlyItsSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);

        var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

        view.Id.ShouldBe(id);
        AgentSessionRuntime.For(view).ShouldNotBeNull();
        host.ConversationOf(id).ShouldBe(host.Broker.Cards);
        host.Notices.ShouldBeEmpty();
        File.Exists(SessionStateLayout.ConfigOverlayFile(Path.Combine(root.Path, "cowork-sessions", id))).ShouldBeTrue();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);

        await host.CloseSessionAsync(id, ct).ConfigureAwait(true);

        host.ConversationOf(id).ShouldBeNull();
        tools.GatewayClosed.ShouldBeFalse();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AClosedToolServerStillOpensAUsableSessionWithADegradedNotice()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await tools.DisposeAsync().ConfigureAwait(true);
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var host = Host(root.Path, new ScriptedOmp(), tools);
            try
            {
                var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);

                var view = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

                view.Id.ShouldBe(id);
                AgentSessionRuntime.For(view).ShouldNotBeNull();
                host.ConversationOf(id).ShouldBe(host.Broker.Cards);
                host.Notices.ShouldHaveSingleItem().ShouldStartWith(CoworkToolServer.UnavailablePrefix);
                tools.Gateway.ShouldBeNull();
                await host.SendAsync(id, "hello", null, ct).ConfigureAwait(true);
                host.OriginsOf(id).ShouldBe([CoworkTurnOrigin.Local]);

                await host.CloseSessionAsync(id, ct).ConfigureAwait(true);

                host.ConversationOf(id).ShouldBeNull();
            }
            finally
            {
                await host.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    [Fact]
    public async Task DuplicateOpensShareOneRuntimeAndReopenGetsAFreshRuntime()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var first = host.OpenSessionAsync(Row(id), ct);
        var second = host.OpenSessionAsync(Row(id), ct);

        var views = await Task.WhenAll(first, second).ConfigureAwait(true);

        views[1].ShouldBeSameAs(views[0]);
        var original = AgentSessionRuntime.For(views[0]).ShouldNotBeNull();
        AgentSessionRuntime.For(views[1]).ShouldBeSameAs(original);
        await host.CloseSessionAsync(id, ct).ConfigureAwait(true);
        host.ConversationOf(id).ShouldBeNull();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);

        var reopened = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

        reopened.ShouldNotBeSameAs(views[0]);
        AgentSessionRuntime.For(reopened).ShouldNotBeNull().ShouldNotBeSameAs(original);
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelledQueuedOpenDoesNotPublishOrDisturbTheOtherSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var firstId = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var cancelledId = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync().ConfigureAwait(true);
        var first = host.OpenSessionAsync(Row(firstId), ct);
        var cancelled = Should.ThrowAsync<OperationCanceledException>(() => host.OpenSessionAsync(Row(cancelledId), cancel.Token));

        var view = await first.ConfigureAwait(true);
        await cancelled.ConfigureAwait(true);

        host.ConversationOf(cancelledId).ShouldBeNull();
        host.ConversationOf(firstId).ShouldBe(host.Broker.Cards);
        (await GatewayStatusAsync(tools, firstId, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
        (await GatewayStatusAsync(tools, cancelledId, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        (await host.OpenSessionAsync(Row(firstId), ct).ConfigureAwait(true)).ShouldBeSameAs(view);

        var recovered = await host.OpenSessionAsync(Row(cancelledId), ct).ConfigureAwait(true);

        recovered.Id.ShouldBe(cancelledId);
        (await GatewayStatusAsync(tools, cancelledId, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ComposeFailureClosesTheAcquiredGatewaySlotAndTheQueueRecovers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var overlay = SessionStateLayout.ConfigOverlayFile(Path.Combine(root.Path, "cowork-sessions", id));
        // A directory at the overlay path forces a real compose I/O failure after gateway acquisition.
        Directory.CreateDirectory(overlay);

        var error = await Record.ExceptionAsync(() => host.OpenSessionAsync(Row(id), ct)).ConfigureAwait(true);

        (error is IOException or UnauthorizedAccessException).ShouldBeTrue();
        host.ConversationOf(id).ShouldBeNull();
        tools.Gateway.ShouldNotBeNull();
        tools.GatewayClosed.ShouldBeFalse();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        Directory.Delete(overlay);

        var recovered = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

        recovered.Id.ShouldBe(id);
        AgentSessionRuntime.For(recovered).ShouldNotBeNull();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CloseQueuedAfterOpenLeavesNoSessionAndAllowsReopen()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var opening = host.OpenSessionAsync(Row(id), ct);
        var closing = host.CloseSessionAsync(id, ct);

        var original = await opening.ConfigureAwait(true);
        await closing.ConfigureAwait(true);

        host.ConversationOf(id).ShouldBeNull();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        var reopened = await host.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);
        reopened.ShouldNotBeSameAs(original);
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignOutConfirmationKeepsSessionsAndConfirmedSignOutDrainsQueuedOpens()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var firstId = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var secondId = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var first = await host.OpenSessionAsync(Row(firstId), ct).ConfigureAwait(true);

        await host.SignOutAsync(false).ConfigureAwait(true);

        host.EnginesStopped.ShouldBeFalse();
        (await host.OpenSessionAsync(Row(firstId), ct).ConfigureAwait(true)).ShouldBeSameAs(first);
        var opening = host.OpenSessionAsync(Row(secondId), ct);
        var signingOut = host.SignOutAsync(true);
        await opening.ConfigureAwait(true);
        await signingOut.ConfigureAwait(true);

        host.EnginesStopped.ShouldBeTrue();
        host.ConversationOf(firstId).ShouldBeNull();
        host.ConversationOf(secondId).ShouldBeNull();
        tools.GatewayClosed.ShouldBeFalse();
        (await GatewayStatusAsync(tools, firstId, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        (await GatewayStatusAsync(tools, secondId, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        var reopened = await host.OpenSessionAsync(Row(firstId), ct).ConfigureAwait(true);
        reopened.ShouldNotBeSameAs(first);
        host.EnginesStopped.ShouldBeFalse();
    }

    [Fact]
    public async Task ConcurrentDisposalDrainsOpenAndLeavesTheBorrowedGatewayAlive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var root = new TempDir();
        await using var tools = new CoworkToolServer();
        await using var host = Host(root.Path, new ScriptedOmp(), tools);
        var id = await host.CreateSessionAsync(ct).ConfigureAwait(true);
        var opening = Record.ExceptionAsync(() => host.OpenSessionAsync(Row(id), ct));
        var firstDisposal = host.DisposeAsync().AsTask();
        var secondDisposal = host.DisposeAsync().AsTask();

        secondDisposal.ShouldBeSameAs(firstDisposal);
        await Task.WhenAll(firstDisposal, secondDisposal).ConfigureAwait(true);
        var openError = await opening.ConfigureAwait(true);

        // Either publication won the race or disposal rejected it; neither may leave a live slot.
        (openError is null or ObjectDisposedException).ShouldBeTrue();
        host.EnginesStopped.ShouldBeTrue();
        host.ConversationOf(id).ShouldBeNull();
        tools.GatewayClosed.ShouldBeFalse();
        (await GatewayStatusAsync(tools, id, ct).ConfigureAwait(true)).ShouldBe(HttpStatusCode.NotFound);
        await Should.ThrowAsync<ObjectDisposedException>(() => host.OpenSessionAsync(Row(id), ct)).ConfigureAwait(true);
    }

    private static CoworkSessionHost Host(string root, ScriptedOmp omp, CoworkToolServer? tools = null) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new Gate(),
            TransportFactory = omp.TransportFactory,
            Tools = tools,
        });

    private static async Task<HttpStatusCode> GatewayStatusAsync(CoworkToolServer tools, string id, CancellationToken cancellationToken)
    {
        var gateway = tools.Gateway ?? throw new InvalidOperationException("The real gateway has not started.");
        var url = new UriBuilder(Uri.UriSchemeHttp, gateway.LocalEndPoint.Address.ToString(), gateway.LocalEndPoint.Port, "/mcp/" + id).Uri;
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"cowork-host-test","version":"1"}}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", gateway.BearerToken);
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(true);
        return response.StatusCode;
    }

    private static AgentRow Row(string id) => new()
    {
        Id = id,
        Title = "task",
        Summary = new SessionSummary { Id = id, Title = "task", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch },
        Status = AgentRowStatus.Ready,
        Group = AgentRowGroup.Idle,
        StatusText = "Ready",
        IconKey = "ready",
        LastActivityText = "now",
        IsLongRunning = false,
        IsStale = false,
        IsLost = false,
        AccessibleName = "task",
    };

    private sealed class Gate : ICoworkAccountGate
    {
        public bool IsSignedIn => true;

        public string? AccountFolderKey => "acct";
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}

#pragma warning restore CA2007
