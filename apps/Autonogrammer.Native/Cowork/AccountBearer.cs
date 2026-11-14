using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Cowork.Projects;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Reads the signed-in account's access token for one request. The token is returned to the caller and is never stored or logged.
/// Signed out, there is no bearer: no other account's token is ever substituted for it.
/// </summary>
internal static class AccountBearer
{
    /// <summary>Returns the active account's token, or null with the reason no token is available.</summary>
    /// <param name="session">The head's account session.</param>
    /// <param name="cancellationToken">Cancels the wait for a refresh.</param>
    /// <returns>The token and <see cref="ProjectSyncState.Synced"/>; or null with <see cref="ProjectSyncState.Offline"/> or <see cref="ProjectSyncState.SignedOut"/>.</returns>
    public static async ValueTask<(string? Token, string State)> ResolveAsync(IAccountSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.State.Phase != AuthPhase.SignedIn)
        {
            return (null, ProjectSyncState.SignedOut);
        }

        var result = await session.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return (result.Value.Reveal(), ProjectSyncState.Synced);
        }

        return (null, result.Failure.Kind == AuthErrorKind.NetworkUnavailable ? ProjectSyncState.Offline : ProjectSyncState.SignedOut);
    }

    /// <summary>Returns the active account's token for a request, or null when there is none.</summary>
    /// <param name="session">The head's account session.</param>
    /// <param name="cancellationToken">Cancels the wait for a refresh.</param>
    /// <returns>The token, or null.</returns>
    public static async ValueTask<string?> TokenOrNullAsync(IAccountSession session, CancellationToken cancellationToken) =>
        (await ResolveAsync(session, cancellationToken).ConfigureAwait(false)).Token;
}
