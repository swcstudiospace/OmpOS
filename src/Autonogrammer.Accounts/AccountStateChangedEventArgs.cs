namespace Autonogrammer.Accounts;

/// <summary>Carries the new <see cref="AccountState"/> of a change.</summary>
public sealed class AccountStateChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="state">The state after the change.</param>
    public AccountStateChangedEventArgs(AccountState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    /// <summary>Gets the state after the change.</summary>
    public AccountState State { get; }
}
