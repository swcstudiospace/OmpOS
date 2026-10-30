using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Cowork;

namespace Autonogrammer.Native;

/// <summary>
/// The signed-in account as Cowork storage sees it. The account subject is hashed into the folder key and is not exposed.
/// </summary>
internal sealed class NativeCoworkAccountGate : ICoworkAccountGate
{
    private readonly IAccountSession _session;

    /// <summary>Creates the gate over the head's account session.</summary>
    /// <param name="session">The session. The gate does not sign in or sign out.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public NativeCoworkAccountGate(IAccountSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    /// <inheritdoc />
    public bool IsSignedIn => _session.State.Phase == AuthPhase.SignedIn && _session.State.Identity is not null;

    /// <inheritdoc />
    public string? AccountFolderKey => IsSignedIn
        ? CoworkAccountFolder.KeyFromSubject(_session.State.Identity!.UserId)
        : null;
}
