namespace Autonogrammer.Presentation.Settings.Privacy;

/// <summary>One statement of the Privacy page: a short heading and the sentence under it.</summary>
/// <param name="Heading">The heading.</param>
/// <param name="Text">The sentence.</param>
public sealed record PrivacyRow(string Heading, string Text)
{
    /// <summary>Gets the whole statement as one sentence for the accessible name.</summary>
    public string Summary => Heading + ". " + Text;
}
