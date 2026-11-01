namespace Autonogrammer.Cowork.Artifacts.Export;

/// <summary>Bytes written for one export. The risk note is set only for <see cref="ArtifactExportChoice.Original"/>.</summary>
public sealed record ArtifactExport
{
    /// <summary>The one-line risk note for an original export.</summary>
    public const string OriginalRiskNote = "Original export keeps the stored bytes unchanged and may contain active content.";

    /// <summary>Gets the file or zip bytes.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>Gets the sanitised file name, with no path separators.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets the risk note when the choice was original.</summary>
    public string? RiskNote { get; init; }
}
