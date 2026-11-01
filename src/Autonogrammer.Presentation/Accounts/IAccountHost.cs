namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// What the account panel needs from a head: sign-in, sign-out, resend, the consent action, and the state to draw. A head maps its session onto this
/// interface. The presentation never sees a token, a secret store or an Accounts type. Expected failures are written into <see cref="State"/>; an
/// implementation must not let a transport exception escape.
/// </summary>
public interface IAccountHost
{
    /// <summary>Occurs when <see cref="State"/> changed. It may be raised on any thread; the model dispatches it.</summary>
    event EventHandler? Changed;

    /// <summary>Gets the state to draw.</summary>
    AccountHostState State { get; }

    /// <summary>Sends a code to an address and moves to awaiting the code, or to the error state.</summary>
    /// <param name="email">The address the person typed.</param>
    /// <param name="cancellationToken">Cancels the request. The panel's Cancel does this while signing in.</param>
    /// <returns>A task that completes when the state has been updated.</returns>
    Task SendCodeAsync(string email, CancellationToken cancellationToken);

    /// <summary>Requests another code for the address already awaiting one.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the state has been updated.</returns>
    Task ResendCodeAsync(CancellationToken cancellationToken);

    /// <summary>Checks a typed code. The host accepts 6 to 10 digits after spaces and hyphens are removed.</summary>
    /// <param name="code">The code as typed.</param>
    /// <param name="cancellationToken">Cancels the check. The panel's Cancel does this while signing in.</param>
    /// <returns>A task that completes when the state has been updated.</returns>
    Task SubmitCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Cancels the sign-in in progress and returns to the previous state.</summary>
    /// <param name="cancellationToken">Cancels the cancel itself only; the local sign-in is abandoned either way.</param>
    /// <returns>A task that completes when the state has been updated.</returns>
    Task CancelAsync(CancellationToken cancellationToken);

    /// <summary>Signs out on this device, or everywhere when <paramref name="everywhere"/> is true.</summary>
    /// <param name="everywhere">True to sign out every session, not only this device.</param>
    /// <param name="cancellationToken">Cancels the notice to the service, never the local sign-out.</param>
    /// <returns>A task that completes when the state has been updated.</returns>
    Task SignOutAsync(bool everywhere, CancellationToken cancellationToken);

    /// <summary>
    /// The consent action offered beside the session-not-remembered notice: remember the sign-in on this device without system protection.
    /// </summary>
    /// <param name="cancellationToken">Cancels the move into the file store.</param>
    /// <returns>A task that completes when the consent has been applied.</returns>
    Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken);

    /// <summary>Leaves the code screen and returns to the address field, abandoning the attempt.</summary>
    void ChangeEmail();

    /// <summary>Reads whether Devices may be shown. A failed read leaves it closed. Startup does not wait for this.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the devices flag has been settled.</returns>
    Task RefreshDevicesAsync(CancellationToken cancellationToken);
}
