using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Sessions;

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

    /// <summary>Initializes a new instance of the <see cref="AcpHarnessSessionFactory"/> class.</summary>
    /// <param name="client">The ACP client that starts omp for the prompt-only overload.</param>
    /// <param name="options">The harness options naming the omp executable.</param>
    /// <param name="repository">The session store that holds each run's tool approvals.</param>
    /// <param name="sessions">The session service that records and settles decisions.</param>
    public AcpHarnessSessionFactory(OmpAcpClient client, IOptions<HarnessOptions> options, ISessionRepository repository, SessionService sessions)
        : this(client, options, repository, sessions, null, AcpApprovalPolicy.DenyAll, PermissionDecisionTimeout)
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
    internal AcpHarnessSessionFactory(
        OmpAcpClient client,
        IOptions<HarnessOptions> options,
        ISessionRepository repository,
        SessionService sessions,
        Func<AcpClientOptions, IAcpTransport>? transportFactory,
        AcpApprovalPolicy approvalPolicy,
        TimeSpan decisionTimeout)
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
        return client.CreateSession(new AcpClientOptions { OmpPath = _options.Value.Acp.OmpPath, ApprovalPolicy = _approvalPolicy });
    }
}
