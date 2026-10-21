using System.Diagnostics.CodeAnalysis;
using OmpOS.Harness.Abstractions;
using OmpOS.Sessions;

namespace OmpOS.Api.Harness;

/// <summary>
/// The store-backed <see cref="IPermissionResolver"/> of one run. The ACP session asks for a decision as soon as it emits a
/// permission request, but the run registers that request in the session store only after it reads the event. The resolver
/// therefore first waits, for at most the registration timeout, until the request is registered. It then waits for the decision
/// through <see cref="SessionService.WaitForDecisionAsync"/>, for at most the decision timeout.
/// </summary>
/// <remarks>
/// Only an allow decision grants. A deny, a lapsed or cancelled wait, a request that never registers, and any failure all return
/// <see cref="PermissionDecision.Deny"/>. The request identifier is <c>{runId}:{RequestId ?? CallId}</c>, the identifier that
/// <c>SessionRunActivities</c> registers for the same request.
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
}
