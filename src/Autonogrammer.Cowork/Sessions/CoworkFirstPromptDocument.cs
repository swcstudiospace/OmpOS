namespace Autonogrammer.Cowork.Sessions;

/// <summary>The on-disk form of one <see cref="CoworkFirstPromptRecord"/>. Unknown members are not kept: the record is rewritten whole.</summary>
internal sealed class CoworkFirstPromptDocument
{
    public int SchemaVersion { get; set; } = 1;

    public string? SessionId { get; set; }

    public string? State { get; set; }

    // The run that wrote an in-flight state, so a later run can tell its own sends from an earlier run's. Null otherwise.
    public string? RunId { get; set; }

    public string? Uri { get; set; }

    public string? MimeType { get; set; }

    public string? Text { get; set; }

    public string? ReplayedText { get; set; }
}
