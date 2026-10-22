using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Sessions;
using OmpOS.Worktrees;

namespace OmpOS.Api.Harness;

/// <summary>
/// <see cref="IHarnessSessionFactory"/> backed by <see cref="OmpAcpClient"/>. Each prompt opens an <c>omp acp</c> session over
/// stdio, started from <c>Harness:Acp:OmpPath</c>. A run opened with its session and run identity gets a client of its own,
/// whose permission resolver is a <see cref="SessionPermissionResolver"/> for that run, so a decision recorded in the session
/// store answers that run's ACP permission request. The prompt-only overload keeps the shared client and its resolver.
/// </summary>
internal sealed class AcpHarnessSessionFactory : IHarnessSessionFactory
{
    /// <summary>
    /// How long an admitted permission request waits for its decision. The ACP session and the store-backed resolver share it,
    /// so both end the wait together and the store records the denial.
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

    /// <summary>Initializes a new instance of the <see cref="AcpHarnessSessionFactory"/> class.</summary>
    /// <param name="client">The ACP client that starts omp for the prompt-only overload.</param>
    /// <param name="options">The harness options naming the omp executable.</param>
    /// <param name="repository">The session store that holds each run's tool approvals.</param>
    /// <param name="sessions">The session service that records and settles decisions.</param>
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset keeps the process working directory.</param>
    public AcpHarnessSessionFactory(OmpAcpClient client, IOptions<HarnessOptions> options, ISessionRepository repository, SessionService sessions, IOptions<WorktreesOptions>? worktrees = null)
        : this(client, options, repository, sessions, null, AcpApprovalPolicy.DenyAll, PermissionDecisionTimeout, worktrees)
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
    /// <param name="worktrees">The worktree settings. When both keys are set, each run works in its own worktree; unset keeps the process working directory.</param>
    internal AcpHarnessSessionFactory(
        OmpAcpClient client,
        IOptions<HarnessOptions> options,
        ISessionRepository repository,
        SessionService sessions,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        AcpApprovalPolicy approvalPolicy,
        TimeSpan decisionTimeout,
        IOptions<WorktreesOptions>? worktrees = null)
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
    }

    /// <inheritdoc />
    public IOmpSession CreateSession(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return _client.CreateSession(new AcpClientOptions { OmpPath = _options.Value.Acp.OmpPath });
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
        if (_worktrees is { } worktrees)
        {
            return new WorktreeRunSession(
                worktrees.Manager,
                worktrees.Repository,
                runId,
                workingDirectory => client.CreateSession(new AcpClientOptions { OmpPath = _options.Value.Acp.OmpPath, WorkingDirectory = workingDirectory, ApprovalPolicy = _approvalPolicy }));
        }

        return client.CreateSession(new AcpClientOptions { OmpPath = _options.Value.Acp.OmpPath, ApprovalPolicy = _approvalPolicy });
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
}
