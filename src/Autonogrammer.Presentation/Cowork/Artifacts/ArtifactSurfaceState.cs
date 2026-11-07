namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The six surface states (D-18).</summary>
public enum ArtifactSurfaceState
{
    /// <summary>No document is loaded.</summary>
    Idle,

    /// <summary>The document is being loaded.</summary>
    Loading,

    /// <summary>The document is shown inside the sandbox.</summary>
    Running,

    /// <summary>The user stopped the document.</summary>
    Stopped,

    /// <summary>A navigation after the first load was cancelled.</summary>
    NavigationBlocked,

    /// <summary>The surface cannot show the document. <see cref="IArtifactSurface.Reason"/> says why.</summary>
    Unavailable,
}
