namespace OmpOS.Workspace.Tests;

/// <summary>
/// A consent store that keeps approvals in memory, with the same one-approval-per-repository rule as the file store.
/// </summary>
internal sealed class MemorySetupConsentStore : ISetupConsentStore
{
    private readonly Dictionary<string, string> _grants = new(StringComparer.Ordinal);

    /// <summary>Gets how many approvals were recorded.</summary>
    public int RecordCount { get; private set; }

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken) =>
        Task.FromResult(_grants.TryGetValue(key.RepositoryPath, out var hash) && string.Equals(hash, key.SetupSha256, StringComparison.Ordinal));

    /// <inheritdoc />
    public Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken)
    {
        _grants[key.RepositoryPath] = key.SetupSha256;
        RecordCount++;
        return Task.CompletedTask;
    }
}
