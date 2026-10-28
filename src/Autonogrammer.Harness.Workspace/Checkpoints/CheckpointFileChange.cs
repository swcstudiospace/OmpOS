namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>The net change one turn made to one file: the content before the turn's first edit and after its last.</summary>
public sealed class CheckpointFileChange
{
    /// <summary>Creates a change.</summary>
    /// <param name="path">The file: absolute inside a workspace folder, or relative to the first workspace folder.</param>
    /// <param name="before">The bytes before the turn, or <see langword="null"/> when the turn created the file.</param>
    /// <param name="after">The bytes after the turn, or <see langword="null"/> when the turn deleted the file.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public CheckpointFileChange(string path, ReadOnlyMemory<byte>? before, ReadOnlyMemory<byte>? after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        Before = before;
        After = after;
    }

    /// <summary>Gets the file as the caller named it.</summary>
    public string Path { get; }

    /// <summary>Gets the bytes before the turn, or <see langword="null"/> when the turn created the file.</summary>
    public ReadOnlyMemory<byte>? Before { get; }

    /// <summary>Gets the bytes after the turn, or <see langword="null"/> when the turn deleted the file.</summary>
    public ReadOnlyMemory<byte>? After { get; }

    /// <summary>Creates a change from the text a transcript's file edit carries.</summary>
    /// <param name="path">The file.</param>
    /// <param name="beforeText">The text before the turn, or <see langword="null"/> when the turn created the file.</param>
    /// <param name="afterText">The text after the turn.</param>
    /// <returns>The change, with the texts as UTF-8 bytes.</returns>
    public static CheckpointFileChange FromText(string path, string? beforeText, string afterText)
    {
        ArgumentNullException.ThrowIfNull(afterText);
        ReadOnlyMemory<byte>? before = null;
        if (beforeText is not null)
        {
            before = System.Text.Encoding.UTF8.GetBytes(beforeText);
        }

        return new CheckpointFileChange(path, before, System.Text.Encoding.UTF8.GetBytes(afterText));
    }
}
