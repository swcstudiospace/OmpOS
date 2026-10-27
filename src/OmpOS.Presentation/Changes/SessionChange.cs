namespace OmpOS.Presentation.Changes;

/// <summary>
/// The net edit of one file across a session: the text before the agent's first edit and the text after its last one.
/// </summary>
public sealed record SessionChange
{
    /// <summary>Initializes a change and counts its added and removed lines.</summary>
    /// <param name="path">The normalised path of the file, relative to the workspace root.</param>
    /// <param name="oldText">The text before the first edit, or null when the first edit created the file.</param>
    /// <param name="newText">The text after the last edit.</param>
    /// <param name="editCount">The number of edit blocks of the session that changed the file.</param>
    /// <param name="lastBlockId">The identifier of the last edit block, which identifies the state of the transcript the change was read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/>, <paramref name="newText"/> or <paramref name="lastBlockId"/> is null.</exception>
    public SessionChange(string path, string? oldText, string newText, int editCount, string lastBlockId)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentNullException.ThrowIfNull(lastBlockId);

        Path = path;
        OldText = oldText;
        NewText = newText;
        EditCount = editCount;
        LastBlockId = lastBlockId;
        (Added, Removed) = ChangeDiff.Count(oldText, newText);
    }

    /// <summary>Gets the normalised path of the file, relative to the workspace root.</summary>
    public string Path { get; }

    /// <summary>Gets the text before the agent's first edit, or null when the agent created the file.</summary>
    public string? OldText { get; }

    /// <summary>Gets the text after the agent's last edit.</summary>
    public string NewText { get; }

    /// <summary>Gets the number of edit blocks of the session that changed the file.</summary>
    public int EditCount { get; }

    /// <summary>Gets the identifier of the last edit block.</summary>
    public string LastBlockId { get; }

    /// <summary>Gets the number of lines the edits added.</summary>
    public int Added { get; }

    /// <summary>Gets the number of lines the edits removed.</summary>
    public int Removed { get; }

    /// <summary>Gets what the edits did to the file.</summary>
    public ChangeKind Kind =>
        OldText is null ? ChangeKind.Created : NewText.Length == 0 && OldText.Length > 0 ? ChangeKind.Emptied : ChangeKind.Edited;

    /// <summary>Gets the same change with another text after the edits, for a file whose hunks were partly undone.</summary>
    /// <param name="newText">The text the file holds now.</param>
    /// <returns>The change, with its counts made again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newText"/> is null.</exception>
    public SessionChange WithNewText(string newText) => new(Path, OldText, newText, EditCount, LastBlockId);
}
