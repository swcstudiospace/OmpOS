using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Vision authority fed by CoworkSessionContext.ModelReadsImages. A switch updates it.</summary>
public sealed class BrowserVisionAuthority
{
    private bool? _override;

    /// <summary>Sets the mid-session model switch.</summary>
    /// <param name="readsImages">Whether the current model reads images.</param>
    public void Set(bool readsImages) => _override = readsImages;

    /// <summary>Reads the current flag.</summary>
    /// <param name="session">The session.</param>
    /// <returns>Whether the model reads images.</returns>
    public bool Reads(CoworkSessionContext session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _override ?? session.ModelReadsImages;
    }
}
