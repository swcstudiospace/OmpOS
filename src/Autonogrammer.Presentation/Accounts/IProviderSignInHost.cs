namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// An optional capability of an account host: sign-in with a third-party provider by a browser redirect. The panel offers the providers the
/// deployment reports as enabled, and nothing else. A host that cannot offer providers does not implement this interface.
/// </summary>
public interface IProviderSignInHost
{
    /// <summary>Reads which providers the deployment has enabled.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The provider names the panel may offer, for example <c>google</c>. Empty when the read failed or the host is not configured.</returns>
    Task<IReadOnlyList<string>> GetEnabledProvidersAsync(CancellationToken cancellationToken);

    /// <summary>Starts a sign-in with a provider in a new browser tab. The host's state changes when the sign-in completes.</summary>
    /// <param name="provider">The provider name the person chose.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>True when the sign-in was started. False when it could not start; the reason is in the host's state.</returns>
    Task<bool> StartProviderSignInAsync(string provider, CancellationToken cancellationToken);
}
