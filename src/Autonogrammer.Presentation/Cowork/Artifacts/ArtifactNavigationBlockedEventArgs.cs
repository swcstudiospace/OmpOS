namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>Carries the target of a navigation the surface cancelled.</summary>
public sealed class ArtifactNavigationBlockedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ArtifactNavigationBlockedEventArgs"/> class.</summary>
    /// <param name="url">The requested target, shown as text only.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public ArtifactNavigationBlockedEventArgs(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Url = url;
    }

    /// <summary>Gets the requested target.</summary>
    public Uri Url { get; }
}
