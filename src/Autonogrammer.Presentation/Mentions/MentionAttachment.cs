namespace Autonogrammer.Presentation.Mentions;

/// <summary>A mention together with the content read for a send. The send path converts it to an embedded ACP resource.</summary>
/// <param name="Mention">The mention.</param>
/// <param name="Content">The content read at send time.</param>
public sealed record MentionAttachment(ResolvedMention Mention, MentionContent Content);
