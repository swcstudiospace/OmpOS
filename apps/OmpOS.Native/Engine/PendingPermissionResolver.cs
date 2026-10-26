using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Engine;

/// <summary>
/// The engine's permission resolver. The ACP session parks each request its approval policy admits and waits here for the
/// decision. The user's decision reaches the request through <see cref="TrySettle"/>, which answers the waiting session once.
/// </summary>
/// <remarks>
/// A request leaves the pending set when it is settled, cancelled or timed out, so a later decision for it finds nothing to
/// settle. The request identifier is the server's <see cref="PermissionRequestEvent.RequestId"/>, or the call identifier when none is sent.
/// </remarks>
internal sealed class PendingPermissionResolver : IPermissionResolver
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TaskCompletionSource<PermissionDecision>> _pending = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    /// <remarks>Waits until <see cref="TrySettle"/> answers the request or the caller cancels. A request identifier that is
    /// already pending is denied, so one request never has two waiters.</remarks>
    public async Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var requestId = request.RequestId ?? request.CallId;
        var answer = new TaskCompletionSource<PermissionDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (!_pending.TryAdd(requestId, answer))
            {
                return PermissionDecision.Deny;
            }
        }

        try
        {
            return await answer.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(requestId, out var current) && ReferenceEquals(current, answer))
                {
                    _pending.Remove(requestId);
                }
            }
        }
    }

    /// <summary>Answers a pending request with <paramref name="decision"/>. Returns false when no request with that identifier is pending.</summary>
    /// <param name="requestId">The request identifier the session parked.</param>
    /// <param name="decision">The decision to answer with.</param>
    /// <returns>True when the request was pending and is now answered; false when it was already settled, cancelled or never parked.</returns>
    public bool TrySettle(string requestId, PermissionDecision decision)
    {
        TaskCompletionSource<PermissionDecision>? answer;
        lock (_gate)
        {
            if (!_pending.Remove(requestId, out answer))
            {
                return false;
            }
        }

        return answer.TrySetResult(decision);
    }
}
