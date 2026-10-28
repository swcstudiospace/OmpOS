namespace OmpOS.Presentation.Mentions;

/// <summary>A chip of the composer: one attached mention, ready to draw.</summary>
/// <param name="Mention">The mention.</param>
/// <param name="Label">The name shown on the chip.</param>
/// <param name="SizeText">The size shown beside the name, or empty when it is not known.</param>
/// <param name="AccessibleName">The name a screen reader speaks for the chip.</param>
/// <param name="RemoveName">The name of the chip's remove button.</param>
public sealed record MentionChip(ResolvedMention Mention, string Label, string SizeText, string AccessibleName, string RemoveName)
{
    /// <summary>Gets the text of the mention in the draft, for example <c>@src/App.cs</c>.</summary>
    public string Token => Mention.Token;

    /// <summary>Gets whether the chip has a size to show.</summary>
    public bool HasSize => SizeText.Length > 0;

    /// <summary>Builds the chip of a mention.</summary>
    /// <param name="mention">The mention.</param>
    /// <returns>The chip.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mention"/> is null.</exception>
    public static MentionChip From(ResolvedMention mention)
    {
        ArgumentNullException.ThrowIfNull(mention);

        return new MentionChip(
            mention,
            mention.Kind == MentionKind.PullRequest ? "#" + mention.Display : mention.Display,
            mention.SizeBytes >= 0 ? MentionCopy.FormatSize(mention.SizeBytes) : string.Empty,
            MentionCopy.ChipName(mention),
            MentionCopy.RemoveName(mention));
    }
}
