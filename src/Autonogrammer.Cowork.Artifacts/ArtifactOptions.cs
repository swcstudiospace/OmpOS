namespace Autonogrammer.Cowork.Artifacts;

/// <summary>
/// Pure limits and panel flags bound from <c>Cowork:Artifacts</c>. Enablement and render-ticket keys belong to the API.
/// </summary>
public sealed class ArtifactOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cowork:Artifacts";

    /// <summary>Gets or sets the per-version cap for markdown, code, HTML and CSV. Default is 512 KiB.</summary>
    public int MaxTextBytes { get; set; } = 512 * 1024;

    /// <summary>Gets or sets the per-version SVG cap. Default is 256 KiB.</summary>
    public int MaxSvgBytes { get; set; } = 256 * 1024;

    /// <summary>Gets or sets the per-version Mermaid cap. Default is 128 KiB.</summary>
    public int MaxMermaidBytes { get; set; } = 128 * 1024;

    /// <summary>Gets or sets the per-version image cap. Default is 5 MiB.</summary>
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Gets or sets the maximum versions of one artifact. Default is 200.</summary>
    public int MaxVersions { get; set; } = 200;

    /// <summary>Gets or sets the maximum artifacts in an account. Default is 500.</summary>
    public int MaxArtifacts { get; set; } = 500;

    /// <summary>Gets or sets the maximum stored bytes in an account. Default is 256 MiB.</summary>
    public long MaxAccountBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>Gets or sets the write cap per session per minute. Default is 20.</summary>
    public int MaxWritesPerMinutePerSession { get; set; } = 20;

    /// <summary>Gets or sets the panel surface name.</summary>
    public string Surface { get; set; } = "panel";

    /// <summary>Gets or sets a value indicating whether the panel may run scripts.</summary>
    public bool Scripts { get; set; }

    /// <summary>Gets or sets a value indicating whether a new artifact opens the panel.</summary>
    public bool AutoOpen { get; set; }
}
