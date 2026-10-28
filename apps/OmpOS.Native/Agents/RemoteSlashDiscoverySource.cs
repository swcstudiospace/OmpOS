using OmpOS.Native.Views.Conventions;
using OmpOS.Presentation.Commands;

namespace OmpOS.Native.Agents;

/// <summary>
/// The custom commands, skills and subagents of a session on an ssh host, read from that host. The first read connects to the host (it composes the session's engine, which makes the ssh resources),
/// so a palette that opens before the first prompt starts the connection.
/// </summary>
internal sealed class RemoteSlashDiscoverySource : ISlashDiscoverySource
{
    private readonly SessionSlot _slot;

    /// <summary>Creates the source over the slot of the remote session.</summary>
    /// <param name="slot">The slot whose engine holds the ssh resources.</param>
    /// <exception cref="ArgumentNullException"><paramref name="slot"/> is null.</exception>
    public RemoteSlashDiscoverySource(SessionSlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        _slot = slot;
    }

    /// <inheritdoc/>
    public async Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken)
    {
        var resources = await _slot.RemoteResourcesAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("This session has no remote host.");
        var setup = await resources.CreateConventionsAsync(cancellationToken).ConfigureAwait(false);
        var set = await setup.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        return ConventionMapping.ToDiscovery(set);
    }
}
