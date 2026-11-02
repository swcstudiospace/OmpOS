namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>The toolbar actions of the artifact panel (D-36).</summary>
public enum ArtifactCommand
{
    /// <summary>Shows the diff against the previous version.</summary>
    Diff,

    /// <summary>Copies the text of a version to the clipboard, on user action.</summary>
    Copy,

    /// <summary>Exports the version or the artifact.</summary>
    Export,

    /// <summary>Creates a new version from the current text.</summary>
    Edit,

    /// <summary>Opens the Access review.</summary>
    Access,

    /// <summary>Stops the running document.</summary>
    Stop,

    /// <summary>Closes the panel.</summary>
    Close,
}
