using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using Autonogrammer.Api.Harness;
using Autonogrammer.Api.IntegrationTests.Harness;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Runs;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// <c>Harness:Adapter=Rpc</c> at the composition root: the RPC factory is selected exhaustively (no fall-through to the
/// in-memory adapter), misconfiguration fails fast naming its key, readiness probes <c>Harness:Rpc:OmpPath</c>, and one run
/// streams to completion through the direct session runner with the RPC factory's scripted seam.
/// </summary>
public sealed class HarnessRpcAdapterTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-rpc-adapter-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_base, "workspaces");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>
    /// Each adapter name selects its own factory; Rpc never falls through to the in-memory factory. The resolved
    /// <see cref="IHarnessSessionFactory"/> is the cloud-routing decorator wrapping the adapter-selected factory, and it
    /// exposes no inner seam, so selection is asserted through the session it mints for a prompt-only (non-cloud) call,
    /// which the decorator delegates to the inner factory unchanged. Creating a session spawns nothing — every adapter
    /// starts its engine lazily on <see cref="IOmpSession.CreateAsync"/> — and the minted sessions are told apart by the
    /// public satellite surfaces hosts bind by casting: an ACP session exposes its engine identity and can fork, an RPC
    /// session exposes its engine identity but cannot fork, and an in-memory session exposes neither. The three pairs are
    /// distinct, so any fall-through to another adapter's factory changes the asserted pair and fails.
    /// </summary>
    /// <param name="adapter">The configured adapter name.</param>
    /// <param name="expectsIdentity">Whether the minted session exposes <see cref="IOmpSessionIdentity"/>.</param>
    /// <param name="expectsFork">Whether the minted session exposes <see cref="IOmpSessionFork"/>.</param>
    [Theory]
    [InlineData(HarnessOptions.RpcAdapter, true, false)]
    [InlineData(HarnessOptions.AcpAdapter, true, true)]
    [InlineData(HarnessOptions.InMemoryAdapter, false, false)]
    public async Task AdapterSelectionResolvesEachFactory(string adapter, bool expectsIdentity, bool expectsFork)
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Harness:Adapter"] = adapter };
        using var factory = new ApiFactory(config);

        var resolved = factory.Services.GetRequiredService<IHarnessSessionFactory>();
        var session = resolved.CreateSession("probe");
        try
        {
            (session is IOmpSessionIdentity).ShouldBe(expectsIdentity);
            (session is IOmpSessionFork).ShouldBe(expectsFork);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>An unknown adapter fails startup, and the failure names Harness:Adapter.</summary>
    [Fact]
    public void UnknownAdapterFailsStartupNamingTheKey()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Harness:Adapter"] = "Bogus" };
        using var factory = new ApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Harness:Adapter");
    }

    /// <summary>Rpc with a blank omp path fails startup, and the failure names Harness:Rpc:OmpPath.</summary>
    [Fact]
    public void RpcWithBlankOmpPathFailsStartupNamingTheKey()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.RpcAdapter,
            ["Harness:Rpc:OmpPath"] = string.Empty,
        };
        using var factory = new ApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Harness:Rpc:OmpPath");
    }

    /// <summary>Rpc combined with configured ACP approval kinds fails startup: RPC has no permission surface to honour them (OQ-525).</summary>
    [Fact]
    public void RpcWithApprovalKindsFailsStartupNamingOq525()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.RpcAdapter,
            ["Harness:Acp:Approval:Kinds:0"] = "read",
        };
        using var factory = new ApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        var failure = StartupFailureText.Of(caught);
        failure.ShouldContain("Harness:Acp:Approval:Kinds");
        failure.ShouldContain("OQ-525");
    }

    /// <summary>A Production host on Rpc without an RPC workspace root (and no worktrees) fails startup naming Harness:Rpc:WorkspaceRoot.</summary>
    [Fact]
    public void ProductionRpcWithoutWorkspaceRootFailsStartupNamingTheRpcKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Adapter"] = HarnessOptions.RpcAdapter;
        using var factory = new ApiFactory(config, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Harness:Rpc:WorkspaceRoot");
    }

    /// <summary>A Production host on Rpc with its workspace root set starts, answers liveness, and the selected factory mints RPC sessions (engine identity, no fork — as in <see cref="AdapterSelectionResolvesEachFactory"/>).</summary>
    [Fact]
    public async Task ProductionWithRpcAdapterStartsAndIsLive()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Adapter"] = HarnessOptions.RpcAdapter;
        config["Harness:Rpc:WorkspaceRoot"] = Root;
        using var factory = new ApiFactory(config, environment: "Production");
        using var client = factory.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Token).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = factory.Services.GetRequiredService<IHarnessSessionFactory>().CreateSession("probe");
        try
        {
            (session is IOmpSessionIdentity).ShouldBeTrue();
            (session is IOmpSessionFork).ShouldBeFalse();
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>The readiness check probes Harness:Rpc:OmpPath: a missing file is unhealthy naming the Rpc key, an existing file is healthy.</summary>
    [Fact]
    public async Task ReadinessCheckProbesTheRpcOmpPath()
    {
        var missing = Path.Combine(_base, "omp");
        var unhealthy = await HarnessReadinessCheckTests.CheckAsync(RpcOptions(missing)).ConfigureAwait(true);
        unhealthy.Status.ShouldBe(HealthStatus.Unhealthy);
        unhealthy.Description.ShouldNotBeNull().ShouldContain("Harness:Rpc:OmpPath");

        var existing = Environment.ProcessPath ?? throw new InvalidOperationException("The test host has no process path.");
        var healthy = await HarnessReadinessCheckTests.CheckAsync(RpcOptions(existing)).ConfigureAwait(true);
        healthy.Status.ShouldBe(HealthStatus.Healthy);
        healthy.Description.ShouldNotBeNull().ShouldContain("Harness:Rpc:OmpPath");
    }

    /// <summary>The ready endpoint answers 503 when the RPC adapter's omp path does not exist, and 200 when it names an existing file.</summary>
    [Fact]
    public async Task ReadyEndpointReflectsTheRpcOmpProbe()
    {
        var missingConfig = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.RpcAdapter,
            ["Harness:Rpc:OmpPath"] = Path.Combine(_base, "omp"),
        };
        using (var missingFactory = new ApiFactory(missingConfig))
        {
            using var client = missingFactory.CreateClient();
            using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }

        var existing = Environment.ProcessPath ?? throw new InvalidOperationException("The test host has no process path.");
        var existingConfig = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.RpcAdapter,
            ["Harness:Rpc:OmpPath"] = existing,
        };
        using var existingFactory = new ApiFactory(existingConfig);
        using var existingClient = existingFactory.CreateClient();
        using var ready = await existingClient.GetAsync(new Uri("/health/ready", UriKind.Relative), Token).ConfigureAwait(true);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// One run over <c>POST /sessions/{id}/runs</c> streams to completion through the direct session runner unchanged, with the
    /// RPC factory minting the run's session through its scripted seam in the session's RPC workspace directory.
    /// </summary>
    [Fact]
    public async Task RunStreamsToCompletionThroughTheDirectRunner()
    {
        var observed = new ConcurrentQueue<OmpRpcClientOptions>();
        var rpcFactory = new OmpRpcHarnessSessionFactory(
            Microsoft.Extensions.Options.Options.Create(new HarnessOptions
            {
                Adapter = HarnessOptions.RpcAdapter,
                Rpc = new HarnessOptions.RpcSettings { OmpPath = "/opt/omp", WorkspaceRoot = Root },
            }),
            options =>
            {
                observed.Enqueue(options);
                return new ScriptedRunSession();
            },
            Microsoft.Extensions.Options.Options.Create(new WorktreesOptions()),
            TestHostEnvironment.Development);
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Harness:Adapter"] = HarnessOptions.RpcAdapter,
            ["Harness:Rpc:WorkspaceRoot"] = Root,
        };
        using var factory = new ApiFactory(config, services => services.AddSingleton<IHarnessSessionFactory>(rpcFactory));
        using var client = factory.CreateClient();
        var sessionId = await SessionClient.CreateSessionAsync(client, Token).ConfigureAwait(true);

        var accepted = await CloudRunClient.CreateRunAsync(client, sessionId, "hello", Token).ConfigureAwait(true);
        var settled = await WaitForRunAsync(client, sessionId, accepted.Id).ConfigureAwait(true);

        settled.Status.ShouldBe(RunStatus.Completed);
        var clientOptions = observed.ShouldHaveSingleItem();
        clientOptions.OmpPath.ShouldBe("/opt/omp");
        clientOptions.Cwd.ShouldBe(Path.Combine(Root, sessionId));
        Directory.Exists(clientOptions.Cwd).ShouldBeTrue();
    }

    private static HarnessOptions RpcOptions(string ompPath)
    {
        var options = new HarnessOptions { Adapter = HarnessOptions.RpcAdapter };
        options.Rpc.OmpPath = ompPath;
        return options;
    }

    private static async Task<Run> WaitForRunAsync(HttpClient client, string sessionId, string runId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, Token);
        while (true)
        {
            var run = await CloudRunClient.GetRunAsync(client, sessionId, runId, linked.Token).ConfigureAwait(true);
            if (run.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), linked.Token).ConfigureAwait(true);
        }
    }

    /// <summary>A scripted RPC-shaped session: one text delta and a completion, as the runner consumes from any transport.</summary>
    private sealed class ScriptedRunSession : IOmpSession
    {
        /// <inheritdoc />
        public HarnessState State => HarnessState.Ready;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport"] = "rpc",
            ["approvals"] = "denied",
        };

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new TextDeltaEvent("rpc: " + prompt);
            yield return new CompletedEvent("end_turn");
        }
    }
}
