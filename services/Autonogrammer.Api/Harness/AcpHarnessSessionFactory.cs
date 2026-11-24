using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Sessions;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// <see cref="IHarnessSessionFactory"/> backed by <see cref="OmpAcpClient"/>. Each prompt opens an <c>omp acp</c> session over
/// stdio, started from <c>Harness:Acp:OmpPath</c>. A run opened with its session and run identity gets a client of its own,
/// whose permission resolver is a <see cref="SessionPermissionResolver"/> for that run, so a decision recorded in the session
/// store answers that run's ACP permission request. The prompt-only overload keeps the shared client and its resolver.
/// Every engine it starts is isolated by <see cref="EngineIsolationSettings"/>: the environment policy goes into each <see cref="AcpClientOptions"/>, and a run
/// without a worktree works in <c>&lt;Harness:Acp:WorkspaceRoot&gt;/&lt;sessionId&gt;</c> instead of the API's working directory.
/// The permission requests that reach a decision are those of the kinds in <c>Harness:Acp:Approval:Kinds</c> (<see cref="HarnessApprovalOptions"/>); with none configured every request is denied at once.
/// </summary>
internal sealed class AcpHarnessSessionFactory : IHarnessSessionFactory
{
    /// <summary>
    /// The default time an admitted permission request waits for its decision (the default of <c>Harness:Acp:Approval:DecisionTimeoutSeconds</c>).
    /// The ACP session and the store-backed resolver share the configured value, so both end the wait together and the store records the denial.
    /// </summary>
    internal static readonly TimeSpan PermissionDecisionTimeout = PermissionDecisionWindow.Default;

    private readonly OmpAcpClient _client;
    private readonly IOptions<HarnessOptions> _options;
    private readonly ISessionRepository _repository;
    private readonly SessionService _sessions;
    private readonly Func<AcpClientOptions, IAcpTransport>? _transportFactory;
    private readonly AcpApprovalPolicy _approvalPolicy;
    private readonly TimeSpan _decisionTimeout;
    private readonly WorktreeRunSettings? _worktrees;
    private readonly bool _development;

    /// <summary>Initializes a new instance of the <see cref="AcpHarnessSessionFactory"/> class.</summary>
    /// <param name="client">The ACP client that starts omp for the prompt-only overload.</param>
    /// <param name="options">The harness options naming the omp executable.</param>
    /// <param name="repository">The session store that holds each run's tool approvals.</param>
    /// <param name="sessions">The session service that records and settles decisions.</param>
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under <c>Harness:Acp:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
    /// <param name="environment">The host environment. Outside Development the engine gets an allow-listed environment by default and needs a working directory of its own. Null is treated as Development.</param>
    public AcpHarnessSessionFactory(OmpAcpClient client, IOptions<HarnessOptions> options, ISessionRepository repository, SessionService sessions, IOptions<WorktreesOptions>? worktrees = null, IHostEnvironment? environment = null)
        : this(client, options, repository, sessions, null, ApprovalOf(options).CreatePolicy(), ApprovalOf(options).DecisionTimeout, worktrees, environment)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpHarnessSessionFactory"/> class with an explicit transport, policy and timeout.</summary>
    /// <param name="client">The ACP client that starts omp for the prompt-only overload.</param>
    /// <param name="options">The harness options naming the omp executable.</param>
    /// <param name="repository">The session store that holds each run's tool approvals.</param>
    /// <param name="sessions">The session service that records and settles decisions.</param>
    /// <param name="transportFactory">Creates the transport of each per-run client. Null spawns <c>omp acp</c>.</param>
    /// <param name="approvalPolicy">The policy that decides which permission requests reach the store-backed resolver.</param>
    /// <param name="decisionTimeout">How long an admitted request waits for its decision. Must be positive.</param>
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under <c>Harness:Acp:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
    /// <param name="environment">The host environment. Outside Development the engine gets an allow-listed environment by default and needs a working directory of its own. Null is treated as Development.</param>
    internal AcpHarnessSessionFactory(
        OmpAcpClient client,
        IOptions<HarnessOptions> options,
        ISessionRepository repository,
        SessionService sessions,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        AcpApprovalPolicy approvalPolicy,
        TimeSpan decisionTimeout,
        IOptions<WorktreesOptions>? worktrees = null,
        IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(approvalPolicy);
        if (decisionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(decisionTimeout), decisionTimeout, "The permission decision timeout must be positive.");
        }

        _client = client;
        _options = options;
        _repository = repository;
        _sessions = sessions;
        _transportFactory = transportFactory;
        _approvalPolicy = approvalPolicy;
        _decisionTimeout = decisionTimeout;
        _worktrees = CreateWorktreeSettings(worktrees?.Value);
        _development = environment is null || environment.IsDevelopment();
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var isolation = Isolation();
        var workingDirectory = isolation.WorkspaceRoot is { } root ? EngineWorkspace.EnsureSessionDirectory(root, PromptDirectoryName()) : null;
        return _client.CreateSession(ClientOptions(isolation, workingDirectory, _approvalPolicy));
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt, string sessionId, string runId)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var resolver = new SessionPermissionResolver(
            _repository,
            _sessions,
            sessionId,
            runId,
            _decisionTimeout,
            SessionPermissionResolver.DefaultRegistrationTimeout);
        var client = _transportFactory is { } transportFactory
            ? new OmpAcpClient(transportFactory, resolver, _decisionTimeout)
            : new OmpAcpClient(resolver, _decisionTimeout);
        var isolation = Isolation();
        if (_worktrees is { } worktrees)
        {
            return new WorktreeRunSession(
                worktrees.Manager,
                worktrees.Repository,
                runId,
                workingDirectory => client.CreateSession(ClientOptions(isolation, workingDirectory, _approvalPolicy)));
        }

        var sessionDirectory = isolation.WorkspaceRoot is { } root ? EngineWorkspace.EnsureSessionDirectory(root, sessionId) : null;
        return client.CreateSession(ClientOptions(isolation, sessionDirectory, _approvalPolicy));
    }

    /// <summary>Reads the <c>Harness:Acp:Approval</c> settings that decide the approval policy and the decision timeout.</summary>
    private static HarnessApprovalOptions ApprovalOf(IOptions<HarnessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value.Acp.Approval;
    }

    private static string PromptDirectoryName() => "prompt-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Resolves the isolation settings. Outside Development an engine that would start in the API's own working directory is refused here as well
    /// as at start-up, so a host without the start-up validation cannot run an engine beside the API's files.
    /// </summary>
    private EngineIsolationSettings Isolation()
    {
        var isolation = EngineIsolationSettings.Resolve(_options.Value.Acp, _development);
        if (!_development && _worktrees is null && isolation.WorkspaceRoot is null)
        {
            throw new InvalidOperationException("Harness:Acp:WorkspaceRoot is required outside the Development environment when run worktrees are not configured.");
        }

        return isolation;
    }

    private AcpClientOptions ClientOptions(EngineIsolationSettings isolation, string? workingDirectory, AcpApprovalPolicy approvalPolicy)
    {
        var options = new AcpClientOptions
        {
            OmpPath = _options.Value.Acp.OmpPath,
            AuthMethodId = AcpClientOptions.OmpAuthMethodId,
            HandshakeTimeout = AcpClientOptions.OmpHandshakeTimeout,
            ApprovalPolicy = approvalPolicy,
            EnvironmentMode = isolation.Mode,
            EnvironmentAllowList = isolation.Allow,
        };
        return workingDirectory is null ? options : options with { WorkingDirectory = workingDirectory };
    }

    private static WorktreeRunSettings? CreateWorktreeSettings(WorktreesOptions? options) =>
        options is not null && !string.IsNullOrWhiteSpace(options.Root) && !string.IsNullOrWhiteSpace(options.Repository)
            ? new WorktreeRunSettings(new WorktreeManager(options.Root), options.Repository)
            : null;

    /// <summary>The worktree manager that creates run worktrees, and the repository each run's worktree is created from.</summary>
    private sealed record WorktreeRunSettings(WorktreeManager Manager, string Repository);
}
