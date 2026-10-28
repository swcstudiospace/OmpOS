namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// One finding about the set being picked.
/// </summary>
/// <param name="IsError">True when the set cannot start a session until it is fixed; false for a warning.</param>
/// <param name="RootId">The row the finding is about, or null for the whole set.</param>
/// <param name="Message">A sentence for the user.</param>
public sealed record WorkspacePickerIssue(bool IsError, string? RootId, string Message);
