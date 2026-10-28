using OmpOS.Presentation.Safety;

namespace OmpOS.Native.Composition;

/// <summary>
/// The Run mode page's source, which refreshes the run mode policy of every open session once the user's choice is saved, so the next tool call is decided under the new mode.
/// </summary>
internal sealed class RefreshingRunModeSource : IRunModeSource
{
    private readonly IRunModeSource _inner;
    private readonly RunModeHub _hub;

    /// <summary>Creates the source.</summary>
    /// <param name="inner">The source that reads and writes the files.</param>
    /// <param name="hub">The hub whose holders are refreshed after a save.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public RefreshingRunModeSource(IRunModeSource inner, RunModeHub hub)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(hub);
        _inner = inner;
        _hub = hub;
    }

    /// <inheritdoc/>
    public Task<RunModeSnapshot> LoadAsync(CancellationToken cancellationToken) => _inner.LoadAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task SaveAsync(RunModeValues values, CancellationToken cancellationToken)
    {
        await _inner.SaveAsync(values, cancellationToken).ConfigureAwait(false);
        await _hub.RefreshAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
