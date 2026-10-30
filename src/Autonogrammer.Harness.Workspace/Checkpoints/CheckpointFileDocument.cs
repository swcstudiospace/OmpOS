namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The persisted form of one <see cref="CheckpointFileEntry"/>. <see cref="BeforeBlob"/> names the stored copy of the earlier content, or is null when none is kept.</summary>
internal sealed class CheckpointFileDocument
{
    public string Root { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? BeforeHash { get; set; }

    public string? BeforeBlob { get; set; }

    public long BeforeSize { get; set; }

    public string? AfterHash { get; set; }

    public long AfterSize { get; set; }

    public string Note { get; set; } = string.Empty;
}
