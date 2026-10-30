using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// The store-backed <see cref="IPermissionResolver"/> of one run. The ACP session asks for a decision as soon as it emits a
/// permission request, but the run registers that request in the session store only after it reads the event. The resolver
/// therefore first waits, for at most the registration timeout, until the request is registered. It then waits for the decision
/// through <see cref="SessionService.WaitForDecisionAsync"/>, for at most the decision timeout.
/// </summary>
/// <remarks>
/// Only an allow decision grants. A deny, a lapsed or cancelled wait, and any failure all return <see cref="PermissionDecision.Deny"/>.
/// A request that no run registers within the registration bound is stored and denied before the agent is answered, so it is never
/// left pending. The request identifier is <c>{runId}:{RequestId ?? CallId}</c>, the identifier that <c>SessionRunActivities</c>
/// registers for the same request.
/// </remarks>
internal sealed class SessionPermissionResolver : IPermissionResolver
{
    /// <summary>How long a permission request may take to reach the session store before it is denied.</summary>
    internal static readonly TimeSpan DefaultRegistrationTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan RegistrationPollInterval = TimeSpan.FromMilliseconds(25);

    private readonly ISessionRepository _repository;
    private readonly SessionService _sessions;
    private readonly string _sessionId;
    private readonly string _runId;
    private readonly TimeSpan _decisionTimeout;
    private readonly TimeSpan _registrationTimeout;

    /// <summary>Initializes a new instance of the <see cref="SessionPermissionResolver"/> class for one run.</summary>
    /// <param name="repository">The session store that holds the run's tool approvals.</param>
    /// <param name="sessions">The session service that waits for and records decisions.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="runId">The run identifier, which scopes the request identifiers of this run.</param>
    /// <param name="decisionTimeout">How long a registered request waits for its decision. Must be positive.</param>
    /// <param name="registrationTimeout">How long a request may take to be registered. Must be positive.</param>
    public SessionPermissionResolver(
        ISessionRepository repository,
        SessionService sessions,
        string sessionId,
        string runId,
        TimeSpan decisionTimeout,
        TimeSpan registrationTimeout)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (decisionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(decisionTimeout), decisionTimeout, "The decision timeout must be positive.");
        }

        if (registrationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(registrationTimeout), registrationTimeout, "The registration timeout must be positive.");
        }

        _repository = repository;
        _sessions = sessions;
        _sessionId = sessionId;
        _runId = runId;
        _decisionTimeout = decisionTimeout;
        _registrationTimeout = registrationTimeout;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The resolver is the fail-closed boundary of the ACP permission path: any failure denies, and never grants.")]
    public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestId = RequestIdOf(request);
        try
        {
            if (!await WaitForRegistrationAsync(requestId, cancellationToken).ConfigureAwait(false))
            {
                await SettleUnregisteredAsync(request, requestId).ConfigureAwait(false);
                return PermissionDecision.Deny;
            }

            var decision = await _sessions.WaitForDecisionAsync(_sessionId, requestId, _decisionTimeout, cancellationToken).ConfigureAwait(false);
            return decision == PermissionDecision.Allow ? PermissionDecision.Allow : PermissionDecision.Deny;
        }
        catch (Exception)
        {
            // A lapsed or cancelled wait is a denial, and WaitForDecisionAsync has already denied the stored request.
            return PermissionDecision.Deny;
        }
    }

    /// <summary>Returns the identifier the run registers for <paramref name="request"/>.</summary>
    private string RequestIdOf(PermissionRequestEvent request) => $"{_runId}:{request.RequestId ?? request.CallId}";

    /// <summary>
    /// Polls the store until the request is registered. Returns false when the registration bound lapses first; a cancelled
    /// caller propagates its cancellation instead.
    /// </summary>
    private async Task<bool> WaitForRegistrationAsync(string requestId, CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(_registrationTimeout);
        try
        {
            while (await _repository.GetToolApprovalAsync(_sessionId, requestId, bound.Token).ConfigureAwait(false) is null)
            {
                await Task.Delay(RegistrationPollInterval, bound.Token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Settles a request that no run registered within the registration bound. The request is stored and denied before the
    /// agent is answered, so it is not pending and a later decision is refused. A run that registers the same request meanwhile
    /// has it denied here. A DiagnosticEvent in the session log records the reason when a request was settled.
    /// </summary>
    private async Task SettleUnregisteredAsync(PermissionRequestEvent request, string requestId)
    {
        try
        {
            await _sessions.RequestToolApprovalAsync(_sessionId, requestId, request.Name, CancellationToken.None).ConfigureAwait(false);
        }
        catch (SessionException)
        {
            // The run registered the request first, or no prompt is running. The denial below settles any pending request.
        }

        try
        {
            await _sessions.ResolveToolApprovalAsync(_sessionId, requestId, approved: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (SessionException)
        {
            // Nothing is pending under this identifier, so there was no request to settle and no reason to record.
            return;
        }

        await RecordUnregisteredDiagnosticAsync(requestId).ConfigureAwait(false);
    }

    /// <summary>Appends a DiagnosticEvent with the reason to the session log, in the turn the request was settled.</summary>
    private async Task RecordUnregisteredDiagnosticAsync(string requestId)
    {
        var session = await _repository.GetAsync(_sessionId, CancellationToken.None).ConfigureAwait(false);
        if (session is null || session.Events.Count == 0)
        {
            return;
        }

        var denial = session.Events[^1];
        await _repository.AppendAsync(
            _sessionId,
            new StreamEvent
            {
                Seq = 0,
                SessionId = _sessionId,
                CorrelationId = denial.CorrelationId,
                Timestamp = TimeProvider.System.GetUtcNow(),
                Payload = new DiagnosticPayload
                {
                    Message = $"Permission request '{requestId}' was not registered within the registration bound; it is denied.",
                },
            },
            CancellationToken.None).ConfigureAwait(false);
    }
}
