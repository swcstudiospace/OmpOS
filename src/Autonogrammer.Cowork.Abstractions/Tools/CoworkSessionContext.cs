namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The session facts the host and providers share. Paths are absolute.</summary>
public sealed record CoworkSessionContext
{
    /// <summary>Gets the session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the absolute working directory.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the absolute trusted folders.</summary>
    public required IReadOnlyList<string> TrustedFolders { get; init; }

    /// <summary>Gets a value indicating whether the current model can read images.</summary>
    public required bool ModelReadsImages { get; init; }
}
