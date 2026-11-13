using System.Collections.Frozen;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Services;
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
/// without a worktree works in <c>&lt;Harness:Acp:WorkspaceRoot&gt;/&lt;account&gt;/&lt;sessionId&gt;</c>, where the account is the session owner's <see cref="AccountWorkspace"/> folder, instead of the API's working directory. The prompt-only overload refuses to start when the workspace root is set.
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
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under its account's folder in <c>Harness:Acp:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
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
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset gives the run its own directory under its account's folder in <c>Harness:Acp:WorkspaceRoot</c>, or keeps the process working directory in Development.</param>
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
        if (isolation.WorkspaceRoot is not null)
        {
            throw new InvalidOperationException("A prompt without a session has no account folder, so Harness:Acp:WorkspaceRoot cannot serve it. Start the run with its session identity.");
        }

        return _client.CreateSession(ClientOptions(isolation, null, _approvalPolicy));
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

        if (isolation.WorkspaceRoot is not { } root)
        {
            return client.CreateSession(ClientOptions(isolation, null, _approvalPolicy));
        }

        if (!EngineWorkspace.IsValidSessionId(sessionId))
        {
            throw new ArgumentException("The session identifier cannot name an engine working directory.", nameof(sessionId));
        }

        return new AccountDirectorySession(_repository, root, sessionId, directory => client.CreateSession(ClientOptions(isolation, directory, _approvalPolicy)));
    }

    /// <summary>Reads the <c>Harness:Acp:Approval</c> settings that decide the approval policy and the decision timeout.</summary>
    private static HarnessApprovalOptions ApprovalOf(IOptions<HarnessOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value.Acp.Approval;
    }

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

    /// <summary>
    /// The harness session of one run in its own git worktree. The worktree is created when the session is created and removed
    /// when the session is disposed, so completion, failure and cancellation all remove it. The omp session is built only after
    /// the worktree exists, because omp starts in that directory and reports it as its working directory.
    /// </summary>
    private sealed class WorktreeRunSession : IOmpSession
    {
        private static readonly IReadOnlyDictionary<string, string> NoCapabilities = FrozenDictionary<string, string>.Empty;

        private readonly WorktreeManager _worktrees;
        private readonly string _repository;
        private readonly string _runId;
        private readonly Func<string, IOmpSession> _openIn;
        private IOmpSession? _session;
        private bool _worktreeCreated;
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="WorktreeRunSession"/> class.</summary>
        /// <param name="worktrees">Creates and removes the run's worktree.</param>
        /// <param name="repository">The repository the worktree is created from.</param>
        /// <param name="runId">The run identifier; it names the worktree directory.</param>
        /// <param name="openIn">Builds the omp session that runs in the given worktree directory.</param>
        public WorktreeRunSession(WorktreeManager worktrees, string repository, string runId, Func<string, IOmpSession> openIn)
        {
            _worktrees = worktrees;
            _repository = repository;
            _runId = runId;
            _openIn = openIn;
        }

        /// <inheritdoc />
        public HarnessState State => _session?.State ?? HarnessState.Disconnected;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => _session?.AdvertisedCapabilities ?? NoCapabilities;

        /// <inheritdoc />
        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await session.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
        {
            var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await session.ResumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
            Connected().SubmitPromptAsync(prompt, options, cancellationToken);

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) =>
            _session is null ? Task.CompletedTask : _session.CancelAsync(cancellationToken);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (_session is not null)
                {
                    await _session.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                if (_worktreeCreated)
                {
                    await _worktrees.CloseAsync(_runId, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private async Task<IOmpSession> OpenAsync(CancellationToken cancellationToken)
        {
            if (_session is null)
            {
                var worktree = await _worktrees.CreateAsync(_repository, _runId, cancellationToken).ConfigureAwait(false);
                _worktreeCreated = true;
                _session = _openIn(worktree.Path);
            }

            return _session;
        }

        private IOmpSession Connected() =>
            _session ?? throw new InvalidOperationException("The run has no worktree yet; CreateAsync must run before a prompt is submitted.");
    }

    /// <summary>
    /// The harness session of one run whose engine works in its account's folder. The owner is read from the session store when the session is first
    /// opened, so the folder is known only after an await. The omp session is built only after the directory exists, because omp starts in that directory.
    /// </summary>
    private sealed class AccountDirectorySession : IOmpSession
    {
        private static readonly IReadOnlyDictionary<string, string> NoCapabilities = FrozenDictionary<string, string>.Empty;

        private readonly ISessionRepository _repository;
        private readonly string _workspaceRoot;
        private readonly string _sessionId;
        private readonly Func<string, IOmpSession> _openIn;
        private IOmpSession? _session;
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="AccountDirectorySession"/> class.</summary>
        /// <param name="repository">The session store that names the session's owner.</param>
        /// <param name="workspaceRoot">The absolute workspace root that holds every account's folder.</param>
        /// <param name="sessionId">The session identifier; it names the working directory under the account's folder.</param>
        /// <param name="openIn">Builds the omp session that runs in the given directory.</param>
        public AccountDirectorySession(ISessionRepository repository, string workspaceRoot, string sessionId, Func<string, IOmpSession> openIn)
        {
            _repository = repository;
            _workspaceRoot = workspaceRoot;
            _sessionId = sessionId;
            _openIn = openIn;
        }

        /// <inheritdoc />
        public HarnessState State => _session?.State ?? HarnessState.Disconnected;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => _session?.AdvertisedCapabilities ?? NoCapabilities;

        /// <inheritdoc />
        public async Task CreateAsync(CancellationToken cancellationToken)
        {
            var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await session.CreateAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
        {
            var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await session.ResumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, CancellationToken cancellationToken) =>
            Connected().SubmitPromptAsync(prompt, options, cancellationToken);

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) =>
            _session is null ? Task.CompletedTask : _session.CancelAsync(cancellationToken);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_session is not null)
            {
                await _session.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task<IOmpSession> OpenAsync(CancellationToken cancellationToken)
        {
            if (_session is null)
            {
                var directory = await DirectoryAsync(cancellationToken).ConfigureAwait(false);
                _session = _openIn(directory);
            }

            return _session;
        }

        /// <summary>Resolves the session's owner and returns the run's working directory under that owner's account folder. Fails closed when either is missing.</summary>
        private async Task<string> DirectoryAsync(CancellationToken cancellationToken)
        {
            var session = await _repository.GetAsync(_sessionId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The session of the run was not found, so its engine working directory cannot be resolved.");
            var accountRoot = AccountWorkspace.Resolve(_workspaceRoot, session.OwnerId)
                ?? throw new InvalidOperationException("Harness:Acp:WorkspaceRoot must be an absolute path for the account's engine working directory.");
            return EngineWorkspace.EnsureSessionDirectory(accountRoot, _sessionId);
        }

        private IOmpSession Connected() =>
            _session ?? throw new InvalidOperationException("The run has no engine yet; CreateAsync must run before a prompt is submitted.");
    }
}
