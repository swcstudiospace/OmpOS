namespace OmpOS.Workspace.Tests;

/// <summary>
/// A consent store whose save always fails, as a read-only configuration directory would.
/// </summary>
internal sealed class UnwritableSetupConsentStore : ISetupConsentStore
{
    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <inheritdoc />
    public Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken) =>
        throw new IOException("the configuration directory is read-only");
}
