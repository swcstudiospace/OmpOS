using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Checks an owner token with <c>GET /workspace</c>, a cheap request every authenticated caller may make, through the same client the rest of the head
/// uses. The token is offered to the session first so the bearer handler sends it, and it is confirmed only when the API answers with success.
/// </summary>
internal sealed class OwnerTokenConnector : IOwnerTokenConnector
{
    /// <summary>How long the check may take. A browser would otherwise wait for its own timeout (100 seconds) with the field locked on "Checking the token".</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly OwnerTokenSession _session;
    private readonly IOmpApiClient _api;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new instance of the <see cref="OwnerTokenConnector"/> class.</summary>
    /// <param name="session">The session that holds the token.</param>
    /// <param name="api">The API client whose requests carry the token.</param>
    /// <param name="timeout">How long the check may take; <see cref="DefaultTimeout"/> when null.</param>
    internal OwnerTokenConnector(OwnerTokenSession session, IOmpApiClient api, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(api);
        _session = session;
        _api = api;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <inheritdoc />
    public async Task<ConnectOutcome> ConnectAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _session.Offer(token);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            _ = await _api.GetWorkspaceAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (ApiProblemException problem) when (problem.Kind is ApiProblemKind.Unauthorized or ApiProblemKind.Forbidden)
        {
            _session.Clear();
            return ConnectOutcome.Rejected;
        }
        catch (ApiProblemException problem) when (problem.Kind is ApiProblemKind.RateLimited)
        {
            // The API is throttling failed attempts from this address: that is not a verdict on the token.
            _session.Clear();
            return ConnectOutcome.RateLimited;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _session.Clear();
            throw;
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is OperationCanceledException)
        {
            // A refused connection, a failing API, a request the browser timed out and the deadline above all mean the token was not checked.
            _session.Clear();
            return ConnectOutcome.Unreachable;
        }

        if (!_session.Confirm())
        {
            return ConnectOutcome.Rejected;
        }

        return ConnectOutcome.Connected;
    }
}
