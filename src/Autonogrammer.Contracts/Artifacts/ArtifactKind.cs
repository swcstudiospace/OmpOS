namespace Autonogrammer.Contracts.Artifacts;

/// <summary>
/// The seven artifact kinds. The set is fixed; no further kind is accepted.
/// </summary>
public enum ArtifactKind
{
    /// <summary>Markdown text.</summary>
    Markdown,

    /// <summary>Source code with a language tag.</summary>
    Code,

    /// <summary>An HTML document.</summary>
    Html,

    /// <summary>An SVG document.</summary>
    Svg,

    /// <summary>A Mermaid diagram.</summary>
    Mermaid,

    /// <summary>A CSV table.</summary>
    Csv,

    /// <summary>A raster image.</summary>
    Image,
}
