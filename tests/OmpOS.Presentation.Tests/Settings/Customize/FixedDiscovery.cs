using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Tests.Settings.Customize;

/// <summary>A discovery source that returns what it was given.</summary>
internal sealed class FixedDiscovery(SlashDiscovery discovery) : ISlashDiscoverySource
{
    /// <inheritdoc/>
    public Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(discovery);
}
