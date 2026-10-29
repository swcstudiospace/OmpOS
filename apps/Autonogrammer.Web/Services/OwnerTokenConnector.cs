using Autonogrammer.UI.Components.Auth;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Checks an owner token with <c>GET /workspace</c>, a cheap request every authenticated caller may make, through the same client the rest of the head
/// uses. The token is offered to the session first so the bearer handler sends it, and it is confirmed only when the API answers with success.
/// </summary>
internal sealed class OwnerTokenConnector : IOwnerTokenConnector
{
    private readonly OwnerTokenSession _session;
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="OwnerTokenConnector"/> class.</summary>
    /// <param name="session">The session that holds the token.</param>
    /// <param name="api">The API client whose requests carry the token.</param>
    internal OwnerTokenConnector(OwnerTokenSession session, IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(api);
        _session = session;
        _api = api;
    }

    /// <inheritdoc />
    public async Task<ConnectOutcome> ConnectAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _session.Offer(token);
        try
        {
            _ = await _api.GetWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ApiProblemException problem) when (problem.Kind is ApiProblemKind.Unauthorized or ApiProblemKind.Forbidden)
        {
            _session.Clear();
            return ConnectOutcome.Rejected;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _session.Clear();
            throw;
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault) || fault is OperationCanceledException)
        {
            // A refused connection, a failing API and a request the browser timed out all mean the token was not checked.
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
