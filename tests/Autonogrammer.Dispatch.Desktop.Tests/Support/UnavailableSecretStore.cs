using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Dispatch.Desktop.Tests.Support;

/// <summary>A secret store whose every operation fails, as a locked operating-system keychain does.</summary>
internal sealed class UnavailableSecretStore : ISecretStore
{
    public Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The keychain is locked.");

    public Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The keychain is locked.");

    public Task<bool> RemoveAsync(string name, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The keychain is locked.");

    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The keychain is locked.");
}
