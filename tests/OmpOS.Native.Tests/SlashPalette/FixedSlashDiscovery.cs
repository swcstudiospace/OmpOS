using OmpOS.Presentation.Commands;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>A discovery source that returns what it was given at once.</summary>
internal sealed class FixedSlashDiscovery(SlashDiscovery discovery) : ISlashDiscoverySource
{
    /// <inheritdoc/>
    public Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(discovery);
}
