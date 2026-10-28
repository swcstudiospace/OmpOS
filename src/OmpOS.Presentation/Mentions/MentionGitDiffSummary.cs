namespace OmpOS.Presentation.Mentions;

/// <summary>What the picker shows about a diff before it is attached.</summary>
/// <param name="Available">Whether the diff can be read.</param>
/// <param name="FileCount">How many files the diff changes; 0 when not available.</param>
/// <param name="BaseRef">The base branch of a branch diff, or <see langword="null"/>.</param>
/// <param name="Reason">Why the diff is not available (no repository, no base branch), or <see langword="null"/>.</param>
public sealed record MentionGitDiffSummary(bool Available, int FileCount, string? BaseRef, string? Reason);
