namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// An optional capability of an account host: sign-in with a browser wallet. Only a head that runs in a browser offers it. The panel shows the
/// wallet choice only when <see cref="WalletAvailableAsync"/> reports a wallet.
/// </summary>
public interface IWalletSignInHost
{
    /// <summary>Reads whether this browser has a wallet the sign-in can use.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when a wallet is present.</returns>
    Task<bool> WalletAvailableAsync(CancellationToken cancellationToken);

    /// <summary>Asks the wallet for an account, has it sign the sign-in message and completes the sign-in.</summary>
    /// <param name="cancellationToken">Cancels the sign-in.</param>
    /// <returns>What happened. Failures the service reports are written into the host's state.</returns>
    Task<WalletSignInOutcome> SignInWithWalletAsync(CancellationToken cancellationToken);
}
