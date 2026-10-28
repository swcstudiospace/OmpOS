namespace Autonogrammer.Presentation.Mentions;

/// <summary>A change of the composer text that a mention caused: the new draft and where the caret goes.</summary>
/// <param name="Draft">The new draft.</param>
/// <param name="Caret">The new caret index.</param>
public sealed record MentionEdit(string Draft, int Caret);
