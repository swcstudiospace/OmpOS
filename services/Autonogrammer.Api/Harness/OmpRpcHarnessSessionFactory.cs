using System.Collections;
using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// <see cref="IHarnessSessionFactory"/> backed by omp in RPC mode through <see cref="RpcHarnessSessionFactory"/>. It mirrors
/// <see cref="AcpHarnessSessionFactory"/>'s engine isolation — the environment policy goes into each
/// <see cref="OmpRpcClientOptions"/>, a run without a worktree works in <c>&lt;Harness:Rpc:WorkspaceRoot&gt;/&lt;sessionId&gt;</c>
/// instead of the API's working directory, and a run with worktrees configured shares <see cref="WorktreeRunSession"/> — minus
/// everything permission-shaped: omp RPC has no observed permission-request surface (OQ-525), so there is no per-run
/// permission resolver to build and nothing to route a decision to. RPC sessions stay deny-by-default and advertise
/// <c>approvals=denied</c>; approval-configured flows stay on the ACP adapter.
/// </summary>
internal sealed class OmpRpcHarnessSessionFactory : IHarnessSessionFactory
{
    private readonly IOptions<HarnessOptions> _options;
    private readonly Func<OmpRpcClientOptions, IOmpSession>? _sessionFactory;
    private readonly WorktreeRunSettings? _worktrees;
    private readonly bool _development;

    /// <summary>Initializes a new instance of the <see cref="OmpRpcHarnessSessionFactory"/> class.</summary>
    /// <param name="options">The harness options naming the omp executable and the RPC isolation keys.</param>
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under <c>Harness:Rpc:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
    /// <param name="environment">The host environment. Outside Development the engine gets an allow-listed environment by default and needs a working directory of its own. Null is treated as Development.</param>
    public OmpRpcHarnessSessionFactory(IOptions<HarnessOptions> options, IOptions<WorktreesOptions>? worktrees = null, IHostEnvironment? environment = null)
        : this(options, null, worktrees, environment)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OmpRpcHarnessSessionFactory"/> class with an explicit session factory.</summary>
    /// <param name="options">The harness options naming the omp executable and the RPC isolation keys.</param>
    /// <param name="sessionFactory">Builds the session for the resolved client options. Null starts omp through a fresh <see cref="OmpRpcClient"/> per session (the working directory is per-client, so one client per session is the design, and its version probe runs at every start).</param>
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under <c>Harness:Rpc:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
    /// <param name="environment">The host environment. Outside Development the engine gets an allow-listed environment by default and needs a working directory of its own. Null is treated as Development.</param>
    internal OmpRpcHarnessSessionFactory(
        IOptions<HarnessOptions> options,
        Func<OmpRpcClientOptions, IOmpSession>? sessionFactory,
        IOptions<WorktreesOptions>? worktrees = null,
        IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _sessionFactory = sessionFactory;
        _worktrees = CreateWorktreeSettings(worktrees?.Value);
        _development = environment is null || environment.IsDevelopment();
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var isolation = Isolation();
        var workingDirectory = isolation.WorkspaceRoot is { } root ? EngineWorkspace.EnsureSessionDirectory(root, PromptDirectoryName()) : null;
        return BuildSession(prompt, ClientOptions(isolation, workingDirectory));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Unlike the ACP factory, this overload deliberately builds no <see cref="SessionPermissionResolver"/>: RPC has no
    /// permission surface to route a decision to (OQ-525), so approvals stay denied. The session and run identity still
    /// name the workspace directory and the run's worktree.
    /// </remarks>
    public IOmpSession CreateSession(string prompt, string sessionId, string runId)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var isolation = Isolation();
        if (_worktrees is { } worktrees)
        {
            return new WorktreeRunSession(
                worktrees.Manager,
                worktrees.Repository,
                runId,
                workingDirectory => BuildSession(prompt, ClientOptions(isolation, workingDirectory)));
        }

        var sessionDirectory = isolation.WorkspaceRoot is { } root ? EngineWorkspace.EnsureSessionDirectory(root, sessionId) : null;
        return BuildSession(prompt, ClientOptions(isolation, sessionDirectory));
    }

    private static string PromptDirectoryName() => "prompt-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Applies the isolation policy to this process's environment. <see cref="EngineEnvironmentMode.Inherit"/> returns
    /// <see langword="null"/>, which <see cref="OmpRpcClientOptions.EnvironmentVariables"/> defines as "inherit unchanged";
    /// <see cref="EngineEnvironmentMode.Allowlist"/> returns exactly the variables the shared ACP policy allows.
    /// </summary>
    private static Dictionary<string, string>? BuildEnvironment(EngineIsolationSettings isolation)
    {
        if (isolation.Mode == EngineEnvironmentMode.Inherit)
        {
            return null;
        }

        var policy = new EngineEnvironmentPolicy(isolation.Mode, isolation.Allow);
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = (string?)entry.Value;
        }

        policy.Apply(environment, Array.Empty<KeyValuePair<string, string>>());
        var filtered = new Dictionary<string, string>(environment.Count, StringComparer.Ordinal);
        foreach (var (name, value) in environment)
        {
            if (value is not null)
            {
                filtered[name] = value;
            }
        }

        return filtered;
    }

    private static WorktreeRunSettings? CreateWorktreeSettings(WorktreesOptions? options) =>
        options is not null && !string.IsNullOrWhiteSpace(options.Root) && !string.IsNullOrWhiteSpace(options.Repository)
            ? new WorktreeRunSettings(new WorktreeManager(options.Root), options.Repository)
            : null;

    /// <summary>
    /// Resolves the isolation settings. Outside Development an engine that would start in the API's own working directory is refused here as well
    /// as at start-up, so a host without the start-up validation cannot run an engine beside the API's files.
    /// </summary>
    private EngineIsolationSettings Isolation()
    {
        var isolation = EngineIsolationSettings.Resolve(_options.Value.Rpc, _development);
        if (!_development && _worktrees is null && isolation.WorkspaceRoot is null)
        {
            throw new InvalidOperationException("Harness:Rpc:WorkspaceRoot is required outside the Development environment when run worktrees are not configured.");
        }

        return isolation;
    }

    private OmpRpcClientOptions ClientOptions(EngineIsolationSettings isolation, string? workingDirectory) => new()
    {
        // Null reaches here only where ACP also starts in the process working directory (Development, or the prompt-only
        // overload when worktrees are configured); RPC names its working directory explicitly because the client requires it.
        Cwd = workingDirectory ?? Directory.GetCurrentDirectory(),
        OmpPath = _options.Value.Rpc.OmpPath,
        EnvironmentVariables = BuildEnvironment(isolation),
    };

    private IOmpSession BuildSession(string prompt, OmpRpcClientOptions clientOptions) =>
        _sessionFactory?.Invoke(clientOptions)
        ?? new RpcHarnessSessionFactory(new OmpRpcClient(clientOptions)).CreateSession(prompt);

    /// <summary>The worktree manager that creates run worktrees, and the repository each run's worktree is created from.</summary>
    private sealed record WorktreeRunSettings(WorktreeManager Manager, string Repository);
}
