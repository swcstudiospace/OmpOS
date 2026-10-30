namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The size limits of a <see cref="CheckpointStore"/>. Every limit bounds what is kept on disk, never what the agent may edit.</summary>
public sealed record CheckpointOptions
{
    /// <summary>The default most bytes of one file whose earlier content is kept: 2 MiB.</summary>
    public const int DefaultMaxFileBytes = 2 * 1024 * 1024;

    /// <summary>The default most bytes of earlier content kept for one session: 256 MiB.</summary>
    public const long DefaultMaxSessionBytes = 256L * 1024 * 1024;

    /// <summary>The default most turns kept for one session.</summary>
    public const int DefaultMaxTurns = 200;

    /// <summary>The default most files recorded for one turn. Files after the limit are not recorded.</summary>
    public const int DefaultMaxFilesPerTurn = 500;

    /// <summary>Gets the most bytes of one file whose content from before a turn is kept. A larger file is recorded by hash only and cannot be restored.</summary>
    public int MaxFileBytes { get; init; } = DefaultMaxFileBytes;

    /// <summary>Gets the most bytes of earlier content kept for one session. The oldest turns are dropped first when it is exceeded.</summary>
    public long MaxSessionBytes { get; init; } = DefaultMaxSessionBytes;

    /// <summary>Gets the most turns kept for one session. The oldest turns are dropped first when it is exceeded.</summary>
    public int MaxTurns { get; init; } = DefaultMaxTurns;

    /// <summary>Gets the most files recorded for one turn.</summary>
    public int MaxFilesPerTurn { get; init; } = DefaultMaxFilesPerTurn;
}
