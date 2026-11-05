using System.Net;
using System.Net.Sockets;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;
using Autonogrammer.Harness.Mcp.Tests.Config;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Mcp.Tests;

/// <summary>Explicit connection checks: tool lists, named failures, timeout, cancellation, and stdio environment policy.</summary>
public sealed class McpConnectionCheckerTests : IDisposable
{
    private const string CanaryNameVariable = "AUTONOGRAMMER_MCP_FIXTURE_CANARY_NAME";

    private readonly List<IDisposable> _owned = [];
    private readonly List<string> _env = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var item in _owned)
        {
            item.Dispose();
        }

        foreach (var name in _env)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public async Task LoopbackFixture_ReturnsConnectedWithTheFixtureTools()
    {
        var log = Track(new CallLogFile());
        using var http = await FixtureHttpProcess.StartAsync(log.Path, Token).ConfigureAwait(true);
        var checker = new McpConnectionChecker();

        var result = await checker.CheckAsync(Http("remote", http.Endpoint), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(McpConnectionOutcome.Connected);
        result.Reason.ShouldBeNull();
        result.Tools.ShouldNotBeNull().Select(tool => tool.Id.ToolName).OrderBy(static name => name, StringComparer.Ordinal).ShouldBe(["add", "echo"]);
        result.ToolCount.ShouldBe(2);
    }

    [Fact]
    public async Task UnreachableServer_ReturnsConnectorCheckFailedWithoutThrowing()
    {
        var checker = new McpConnectionChecker(new McpConnectionCheckOptions { Timeout = TimeSpan.FromSeconds(2) });

        var result = await checker.CheckAsync(Http("gone", new Uri("http://127.0.0.1:9/mcp")), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(McpConnectionOutcome.Failed);
        result.Reason.ShouldBe(McpConnectionChecker.ConnectorCheckFailed);
        result.Tools.ShouldBeNull();
    }

    [Fact]
    public async Task Timeout_ReturnsConnectorCheckTimedOut_AndCancellationAborts()
    {
        using var hang = new HangListener();
        var slow = new McpConnectionChecker(new McpConnectionCheckOptions { Timeout = TimeSpan.FromMilliseconds(300) });
        var timed = await slow.CheckAsync(Http("hang", hang.Endpoint), Token).ConfigureAwait(true);
        timed.Outcome.ShouldBe(McpConnectionOutcome.Failed);
        timed.Reason.ShouldBe(McpConnectionChecker.ConnectorCheckTimedOut);

        var patient = new McpConnectionChecker(new McpConnectionCheckOptions { Timeout = TimeSpan.FromSeconds(30) });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(100));
        var started = DateTime.UtcNow;
        await Should.ThrowAsync<OperationCanceledException>(() => patient.CheckAsync(Http("hang", hang.Endpoint), cancel.Token)).ConfigureAwait(true);
        (DateTime.UtcNow - started).ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConstructingTheCatalog_DoesNotCheck()
    {
        var fake = new FakeMcpConnectionChecker(_ => new McpConnectionResult(McpConnectionOutcome.Connected));
        _ = new McpServerCatalog(new MemoryMcpFileStore(), new MemoryMcpState(), fake);

        fake.Checked.ShouldBeEmpty();
    }

    [Fact]
    public async Task StdioEnvironment_FollowsModeBundleAndSpecEnv()
    {
        var parent = "AG_MCP_PARENT_" + Guid.NewGuid().ToString("N");
        var canary = TrackEnv(parent);
        Environment.SetEnvironmentVariable(canary, "1");
        var specEnv = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CanaryNameVariable] = canary,
        };

        var stripped = await CheckStdioAsync(EngineEnvironmentMode.Allowlist, "~/.autonogrammer/mcp.json", specEnv).ConfigureAwait(true);
        stripped.ShouldBe("canary_unset");

        var inherited = await CheckStdioAsync(EngineEnvironmentMode.Inherit, "~/.autonogrammer/mcp.json", specEnv).ConfigureAwait(true);
        inherited.ShouldBe("canary_set");

        var bundled = await CheckStdioAsync(EngineEnvironmentMode.Inherit, "plugin:demo", specEnv).ConfigureAwait(true);
        bundled.ShouldBe("canary_unset");

        var applied = new Dictionary<string, string>(specEnv, StringComparer.Ordinal) { [canary] = "from-spec" };
        var last = await CheckStdioAsync(EngineEnvironmentMode.Allowlist, "~/.autonogrammer/mcp.json", applied).ConfigureAwait(true);
        last.ShouldBe("canary_set");
    }

    private static async Task<string> CheckStdioAsync(EngineEnvironmentMode mode, string origin, IReadOnlyDictionary<string, string> env)
    {
        var checker = new McpConnectionChecker(new McpConnectionCheckOptions
        {
            EnvironmentMode = mode,
            EnvironmentAllowList = ["DOTNET_*"],
        });
        var result = await checker.CheckAsync(Stdio(origin, env), Token).ConfigureAwait(true);
        result.Outcome.ShouldBe(McpConnectionOutcome.Connected, result.Reason);
        return result.Tools.ShouldNotBeNull().Select(tool => tool.Id.ToolName).Single(name => name.StartsWith("canary_", StringComparison.Ordinal));
    }

    private static McpServerSpec Http(string name, Uri url) => new()
    {
        Name = name,
        Transport = McpTransportKind.Http,
        Scope = McpScope.User,
        Format = McpConfigFormat.Autonogrammer,
        Origin = "~/.autonogrammer/mcp.json",
        Priority = 1,
        Url = url,
        ConsentIdentity = "consent",
    };

    private static McpServerSpec Stdio(string origin, IReadOnlyDictionary<string, string> env) => new()
    {
        Name = "stdio",
        Transport = McpTransportKind.Stdio,
        Scope = McpScope.User,
        Format = McpConfigFormat.Autonogrammer,
        Origin = origin,
        Priority = 1,
        Command = McpFixtureProcess.Executable,
        Args = [],
        Env = env,
        ConsentIdentity = "consent",
    };

    private string TrackEnv(string name)
    {
        _env.Add(name);
        return name;
    }

    private T Track<T>(T item)
        where T : IDisposable
    {
        _owned.Add(item);
        return item;
    }

    private sealed class HangListener : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly System.Collections.Concurrent.ConcurrentBag<TcpClient> _accepted = new();
        private bool _disposed;

        public HangListener()
        {
            _listener.Start();
            Endpoint = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/mcp");
            _ = AcceptAsync(_stop.Token);
        }

        public Uri Endpoint { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _stop.Cancel();
            _listener.Stop();
            _listener.Dispose();
            while (_accepted.TryTake(out var accepted))
            {
                accepted.Dispose();
            }
            _stop.Dispose();
        }

        private async Task AcceptAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var accepted = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _accepted.Add(accepted);
                }
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }
        }
    }

    private sealed class MemoryMcpState : IMcpStateStore
    {
        public Task<McpState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(McpState.Empty);

        public Task SaveAsync(McpState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
