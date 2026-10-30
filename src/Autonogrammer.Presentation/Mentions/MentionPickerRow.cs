namespace Autonogrammer.Presentation.Mentions;

/// <summary>One row of the picker, ready to draw.</summary>
/// <param name="Index">The row's position in the flat list; it is the option index of the combobox.</param>
/// <param name="Candidate">What the row attaches.</param>
/// <param name="Label">The text shown: the name, with the <c>#</c> of a pull request.</param>
/// <param name="AutomationName">The name a screen reader speaks.</param>
/// <param name="IsGroupStart">Whether the row is the first of its group, so the view draws the group title above it.</param>
/// <param name="GroupTitle">The title of the row's group.</param>
public sealed record MentionPickerRow(int Index, MentionCandidate Candidate, string Label, string AutomationName, bool IsGroupStart, string GroupTitle)
{
    /// <summary>Gets the identifier of the row as an option: the value of the combobox's active descendant while the row is selected.</summary>
    public string OptionId => MentionPickerModel.OptionIdOf(Index);

    /// <summary>Gets the secondary text, or <see langword="null"/>.</summary>
    public string? Detail => Candidate.DisabledReason ?? Candidate.Detail;

    /// <summary>Gets whether the row has a secondary text.</summary>
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>Gets whether the row can be attached.</summary>
    public bool IsEnabled => Candidate.IsEnabled;
}
