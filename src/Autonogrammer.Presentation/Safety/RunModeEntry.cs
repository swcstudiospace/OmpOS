namespace Autonogrammer.Presentation.Safety;

/// <summary>One line of a list on the Run mode page: the entry, the file it comes from, and whether this page can remove it.</summary>
/// <param name="Text">The entry.</param>
/// <param name="Source">The path of the file that holds it, or an empty string when it is not in a file.</param>
/// <param name="IsEditable">True when the entry is in the Autonogrammer file this page edits.</param>
public sealed record RunModeEntry(string Text, string Source, bool IsEditable)
{
    /// <summary>Gets the line as one sentence for an accessible name.</summary>
    public string Summary => Source.Length == 0 ? Text : Text + ", from " + Source;
}
