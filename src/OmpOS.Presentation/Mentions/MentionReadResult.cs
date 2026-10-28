namespace OmpOS.Presentation.Mentions;

/// <summary>The result of reading every attached mention for a send.</summary>
/// <param name="Attachments">The mentions that were read, in chip order.</param>
/// <param name="Failures">The names of the mentions that could not be read.</param>
public sealed record MentionReadResult(IReadOnlyList<MentionAttachment> Attachments, IReadOnlyList<string> Failures);
