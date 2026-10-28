namespace OmpOS.Presentation.Mentions;

/// <summary>The rows of one group of the picker.</summary>
/// <param name="Title">The group title, for example <c>Files</c>.</param>
/// <param name="Rows">The rows of the group, best first.</param>
public sealed record MentionGroup(string Title, IReadOnlyList<MentionPickerRow> Rows);
