using OmpOS.Workspace;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>A setup consent store that keeps its approvals in memory. Like the real store, a new approval replaces the earlier one of the same repository.</summary>
internal sealed class InMemorySetupConsentStore : ISetupConsentStore
{
    private readonly Dictionary<string, string> _granted = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Task.FromResult(_granted.TryGetValue(key.RepositoryPath, out var hash) && string.Equals(hash, key.SetupSha256, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        _granted[key.RepositoryPath] = key.SetupSha256;
        return Task.CompletedTask;
    }
}
