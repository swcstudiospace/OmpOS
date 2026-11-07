using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts;

/// <summary>
/// The only view of the signed-in account that other code needs: the state, a token for the API, and sign-out. Heads and the modules that need an
/// account (Cowork, Dispatch) use this interface; only <see cref="AccountSessionManager"/> holds the session.
/// </summary>
public interface IAccountSession
{
    /// <summary>Occurs after <see cref="State"/> changed. Handlers run on the thread that made the change.</summary>
    event EventHandler<AccountStateChangedEventArgs>? StateChanged;

    /// <summary>Gets the current state.</summary>
    AccountState State { get; }

    /// <summary>
    /// Returns an access token for the API, refreshing first when its remaining life is below the larger of 60 seconds and a fifth of its lifetime.
    /// All concurrent callers share one refresh. It does not throw for expected failures.
    /// </summary>
    /// <param name="cancellationToken">Stops this caller's wait; the shared refresh continues.</param>
    /// <returns>The token, or why none is available: <see cref="AuthErrorKind.SessionEnded"/> when signed out, <see cref="AuthErrorKind.NetworkUnavailable"/> when it has expired and the service cannot be reached.</returns>
    ValueTask<AuthResult<SecretString>> GetAccessTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Signs out. For <see cref="SignOutScope.Local"/> and <see cref="SignOutScope.Global"/> the local state and the stored secret are cleared first,
    /// then the service is told as a best effort. For <see cref="SignOutScope.Others"/> this session stays signed in.
    /// </summary>
    /// <param name="scope">How far the sign-out reaches. There is no default.</param>
    /// <param name="cancellationToken">Cancels the notification to the service, never the local clearing.</param>
    /// <returns>Success when the service confirmed, otherwise why it did not. The device is signed out either way for local and global scope.</returns>
    Task<AuthResult<AuthUnit>> SignOutAsync(SignOutScope scope, CancellationToken cancellationToken);
}
