using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the life of a server: what start publishes (URL, token, environment, startup file), what dispose does (stops
/// listening, answers requests in flight with 503, cancels their handlers, deletes the startup file) and that starting and
/// disposing in the wrong order or twice is handled.
/// </summary>
public sealed class HookBridgeLifecycleTests
{
    private static readonly TimeSpan Safety = TimeSpan.FromSeconds(30);

    private static HashSet<string> Names(params string[] names) => new(names, StringComparer.Ordinal);

    private static string[] StringsOf(JsonElement array) => [.. array.EnumerateArray().Select(static element => element.GetString()!)];

    [Fact]
    public async Task StartBindsTheIPv4LoopbackOnAnEphemeralPortAndPublishesTheUrlAndAFreshToken()
    {
        var first = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision())).ConfigureAwait(true);
        await using var firstLifetime = first.ConfigureAwait(true);
        var second = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision())).ConfigureAwait(true);
        await using var secondLifetime = second.ConfigureAwait(true);

        first.Server.Url.Scheme.ShouldBe("http");
        first.Server.Url.Host.ShouldBe("127.0.0.1");
        first.Server.Url.AbsolutePath.ShouldBe("/v1/hook");
        first.Server.Url.Query.ShouldBeEmpty();
        first.Port.ShouldBeGreaterThan(0);
        first.Port.ShouldNotBe(second.Port);
        first.Token.Length.ShouldBe(43);
        first.Token.ShouldNotBe(second.Token);
    }

    [Fact]
    public async Task TheEnvironmentHoldsTheUrlAndTheTokenAndNoStartupFileVariableWhenNoFileIsConfigured()
    {
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision())).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var environment = rig.Server.Environment;

        environment.Keys.ShouldBe(["OMPOS_HOOKS_URL", "OMPOS_HOOKS_TOKEN"], ignoreOrder: true);
        environment["OMPOS_HOOKS_URL"].ShouldBe(rig.Server.Url.AbsoluteUri);
        environment["OMPOS_HOOKS_URL"].ShouldBe("http://127.0.0.1:" + rig.Port.ToString(CultureInfo.InvariantCulture) + "/v1/hook");
        environment["OMPOS_HOOKS_TOKEN"].ShouldBe(rig.Token);
        HookBridgeEnvironment.Url.ShouldBe("OMPOS_HOOKS_URL");
        HookBridgeEnvironment.Token.ShouldBe("OMPOS_HOOKS_TOKEN");
        HookBridgeEnvironment.StartupFile.ShouldBe("OMPOS_HOOKS_STARTUP_FILE");
    }

    [Fact]
    public async Task TheStartupFileHoldsTheSortedEventListsAndNeverTheTokenAndTheEnvironmentNamesIt()
    {
        using var directory = new PluginFixtureDirectory();
        var path = directory.PathOf("state/hooks/startup.json");
        var options = new HookBridgeOptions
        {
            StartupFilePath = path,
            FailClosedEvents = Names("preToolUse", "beforeShellExecution"),
            ActiveEvents = Names("stop", "preToolUse", "beforeShellExecution"),
            ActiveOmpEvents = Names("tool_call"),
        };
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision()), options).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true);

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        root.EnumerateObject().Select(static property => property.Name).ShouldBe(["version", "failClosedEvents", "activeEvents", "activeOmpEvents", "requestTimeoutMs"], ignoreOrder: true);
        root.GetProperty("version").GetInt32().ShouldBe(1);
        root.GetProperty("requestTimeoutMs").GetInt32().ShouldBe(130_000);
        StringsOf(root.GetProperty("failClosedEvents")).ShouldBe(["beforeShellExecution", "preToolUse"]);
        StringsOf(root.GetProperty("activeEvents")).ShouldBe(["beforeShellExecution", "preToolUse", "stop"]);
        StringsOf(root.GetProperty("activeOmpEvents")).ShouldBe(["tool_call"]);
        text.ShouldNotContain(rig.Token, Case.Sensitive);
        rig.Server.Environment["OMPOS_HOOKS_STARTUP_FILE"].ShouldBe(Path.GetFullPath(path));
        Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName).ShouldBe(["startup.json"]);
    }

    [Fact]
    public async Task AnEmptyConfigurationWritesEmptyListsSoTheExtensionSkipsEveryEvent()
    {
        using var directory = new PluginFixtureDirectory();
        var path = directory.PathOf("startup.json");
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision()), new HookBridgeOptions { StartupFilePath = path }).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true));

        document.RootElement.GetProperty("failClosedEvents").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("activeEvents").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("activeOmpEvents").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task TheRequestTimeoutInTheStartupFileIsTheHandlerTimeoutInMillisecondsPlusTenSeconds()
    {
        using var directory = new PluginFixtureDirectory();
        var path = directory.PathOf("startup.json");
        var options = new HookBridgeOptions { StartupFilePath = path, HandlerTimeout = TimeSpan.FromMilliseconds(5_250) };
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision()), options).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true));

        document.RootElement.GetProperty("requestTimeoutMs").GetInt32().ShouldBe(15_250);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task TheStartupFileIsOwnerOnlyAndItsNewDirectoryIsOwnerOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var directory = new PluginFixtureDirectory();
        var path = directory.PathOf("state/hooks/startup.json");
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision()), new HookBridgeOptions { StartupFilePath = path }).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(Path.GetDirectoryName(path)!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task DisposeDeletesTheStartupFileAndKeepsItsDirectoryAndIsRepeatable()
    {
        using var directory = new PluginFixtureDirectory();
        var path = directory.PathOf("state/startup.json");
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision()), new HookBridgeOptions { StartupFilePath = path }).ConfigureAwait(true);
        File.Exists(path).ShouldBeTrue();

        await rig.DisposeAsync().ConfigureAwait(true);
        await rig.DisposeAsync().ConfigureAwait(true);

        File.Exists(path).ShouldBeFalse();
        Directory.Exists(Path.GetDirectoryName(path)).ShouldBeTrue();
    }

    [Fact]
    public async Task AStartupFileThatCannotBeWrittenFailsTheStartAndLeavesNoServerBehind()
    {
        using var directory = new PluginFixtureDirectory();
        var blocker = directory.WriteFile("blocker", "a file where a directory is needed");
        var server = new HookBridgeServer(
            FakeBridgeHandler.Returning(new BridgeDecision()),
            new HookBridgeOptions { StartupFilePath = Path.Combine(blocker, "startup.json") });

        await Should.ThrowAsync<IOException>(() => server.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        await Should.ThrowAsync<ObjectDisposedException>(() => server.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
        await server.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AfterDisposeNobodyListensAnymore()
    {
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision())).ConfigureAwait(true);
        var port = rig.Port;
        await rig.DisposeAsync().ConfigureAwait(true);
        using var client = new TcpClient();

        var failure = await Should.ThrowAsync<SocketException>(() => client.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);

        failure.SocketErrorCode.ShouldBe(SocketError.ConnectionRefused);
    }

    [Fact]
    public async Task DisposeAnswersARequestInFlightWith503AndCancelsItsHandler()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeBridgeHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            try
            {
                await never.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                cancelled.TrySetResult();
                throw;
            }

            return new BridgeDecision();
        });
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync("{\"event\":\"preToolUse\"}");
        await entered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);

        await rig.DisposeAsync().ConfigureAwait(true);
        var response = await pending.ConfigureAwait(true);

        response.Status.ShouldBe(503);
        response.Body.ShouldBe("{\"error\":\"session_closed\"}");
        await cancelled.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task DisposeAnswersARequestWaitingForAnApprovalWith503()
    {
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prompt = FakeApprovalPrompt.Running(async (_, _, cancellationToken) =>
        {
            asked.TrySetResult();
            await never.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
            return true;
        });
        var handler = FakeBridgeHandler.Returning(new BridgeDecision { Permission = BridgeDecision.PermissionAsk });
        var rig = await HookBridgeRig.StartAsync(handler, prompt: prompt).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync("{\"event\":\"beforeShellExecution\"}");
        await asked.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);

        await rig.DisposeAsync().ConfigureAwait(true);
        var response = await pending.ConfigureAwait(true);

        response.Status.ShouldBe(503);
    }

    [Fact]
    public async Task DisposeDoesNotWaitForAHandlerThatIgnoresItsToken()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var testToken = TestContext.Current.CancellationToken;
        var handler = new FakeBridgeHandler(async (_, _) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(Safety, testToken).ConfigureAwait(true);
            return new BridgeDecision();
        });
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);
        var pending = rig.PostAsync("{\"event\":\"stop\"}");
        await entered.Task.WaitAsync(Safety, TestContext.Current.CancellationToken).ConfigureAwait(true);

        await rig.DisposeAsync().ConfigureAwait(true);
        var response = await pending.ConfigureAwait(true);
        release.SetResult();

        response.Status.ShouldBe(503);
    }

    [Fact]
    public async Task ADisposedServerCannotBeStartedAndAServerThatWasNotStartedHasNoUrlOrEnvironment()
    {
        var server = new HookBridgeServer(FakeBridgeHandler.Returning(new BridgeDecision()), new HookBridgeOptions());

        Should.Throw<InvalidOperationException>(() => { _ = server.Url; });
        Should.Throw<InvalidOperationException>(() => { _ = server.Environment; });
        await server.DisposeAsync().ConfigureAwait(true);
        await Should.ThrowAsync<ObjectDisposedException>(() => server.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task AServerCanBeStartedOnlyOnce()
    {
        var rig = await HookBridgeRig.StartAsync(FakeBridgeHandler.Returning(new BridgeDecision())).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => rig.Server.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);

        (await rig.PostAsync("{\"event\":\"stop\"}").ConfigureAwait(true)).Status.ShouldBe(200);
    }

    [Fact]
    public async Task AStartThatIsCancelledBeforeItBeginsChangesNothingAndTheServerCanStillBeStarted()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        var server = new HookBridgeServer(FakeBridgeHandler.Returning(new BridgeDecision()), new HookBridgeOptions());
        await using var lifetime = server.ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => server.StartAsync(cancelled.Token)).ConfigureAwait(true);
        await server.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        server.Url.Port.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void OptionsOutOfRangeAreRejectedWhenTheServerIsCreated()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());

        Should.Throw<ArgumentNullException>(() => new HookBridgeServer(null!, new HookBridgeOptions()));
        Should.Throw<ArgumentNullException>(() => new HookBridgeServer(handler, null!));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { MaxRequestBytes = 0 }));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { MaxHeaderBytes = 255 }));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { MaxConcurrentRequests = 0 }));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { HandlerTimeout = TimeSpan.Zero }));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { HandlerTimeout = TimeSpan.FromDays(2) }));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookBridgeServer(handler, new HookBridgeOptions { ReadTimeout = TimeSpan.FromSeconds(-1) }));
        Should.Throw<ArgumentException>(() => new HookBridgeServer(handler, new HookBridgeOptions { StartupFilePath = " " }));
    }

    [Fact]
    public void TheDocumentedDefaultsAreTheContractValues()
    {
        var options = new HookBridgeOptions();

        options.MaxRequestBytes.ShouldBe(8 * 1024 * 1024);
        options.MaxHeaderBytes.ShouldBe(16 * 1024);
        options.HandlerTimeout.ShouldBe(TimeSpan.FromSeconds(120));
        options.MaxConcurrentRequests.ShouldBe(64);
        options.ReadTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.FailClosedEvents.ShouldBeEmpty();
        options.ActiveEvents.ShouldBeEmpty();
        options.ActiveOmpEvents.ShouldBeEmpty();
        options.StartupFilePath.ShouldBeNull();
    }
}
