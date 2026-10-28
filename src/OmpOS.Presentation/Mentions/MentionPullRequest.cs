namespace OmpOS.Presentation.Mentions;

/// <summary>A pull request a mention can attach.</summary>
/// <param name="Number">The pull request number.</param>
/// <param name="Title">The title.</param>
/// <param name="State">The state, for example <c>open</c>, <c>merged</c> or <c>closed</c>.</param>
public sealed record MentionPullRequest(int Number, string Title, string State);
