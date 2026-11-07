namespace Autonogrammer.Cowork.Artifacts.Export;

/// <summary>Whether export applies the safe default or returns the stored bytes.</summary>
public enum ArtifactExportChoice
{
    /// <summary>Sanitised SVG, neutralised CSV, and HTML with the panel meta policy.</summary>
    Safe,

    /// <summary>Stored bytes, with a one-line risk note.</summary>
    Original,
}
