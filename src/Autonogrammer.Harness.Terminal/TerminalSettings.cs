namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Explicit terminal configuration. The defaults keep every command behind an approval.
/// </summary>
public sealed record TerminalSettings
{
    /// <summary>
    /// When true, proposals run without waiting for an approval. Off by default; set only by explicit configuration.
    /// </summary>
    public bool AutoRunEnabled { get; init; }

    /// <summary>
    /// The largest number of characters kept for each of standard output and standard error.
    /// </summary>
    public int MaxOutputCharacters { get; init; } = 64 * 1024;
}
