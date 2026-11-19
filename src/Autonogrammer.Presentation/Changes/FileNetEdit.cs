using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Presentation.Changes;

/// <summary>
/// One file's net edit across the tool calls that changed it: the text before its first edit and the text after its last. It holds the
/// net-edit rule both the session's and the per-turn change sets follow: a file whose last text equals its first has no net change.
/// </summary>
internal sealed class FileNetEdit(string path, string? oldText)
{
    /// <summary>Gets the path of the file, relative to the workspace root.</summary>
    public string Path { get; } = path;

    /// <summary>Gets the text before the first edit, or null when the first edit created the file.</summary>
    public string? OldText { get; } = oldText;

    /// <summary>Gets the text after the last edit.</summary>
    public string NewText { get; private set; } = string.Empty;

    /// <summary>Gets the id of the block of the last edit.</summary>
    public string LastBlockId { get; private set; } = string.Empty;

    /// <summary>Gets the number of edits the file received.</summary>
    public int EditCount { get; private set; }

    /// <summary>Gets a value indicating whether the file differs from its text before the first edit. A created file always does.</summary>
    public bool HasNetChange => OldText is null || !string.Equals(OldText, NewText, StringComparison.Ordinal);

    /// <summary>Records one more edit of the file, after the ones already recorded.</summary>
    /// <param name="edit">The edit.</param>
    public void Add(FileEditBlock edit)
    {
        NewText = edit.NewText;
        LastBlockId = edit.Id;
        EditCount++;
    }
}
