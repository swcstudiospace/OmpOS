namespace Autonogrammer.Presentation.Landing;

/// <summary>One environment card of the Code landing.</summary>
/// <param name="Kind">Which card this is.</param>
/// <param name="State">The state the card is in.</param>
/// <param name="Title">The name of the environment.</param>
/// <param name="Message">The one message of the state.</param>
/// <param name="OmpText">The Omp status line: the version and source, why omp is missing, or that it is not checked.</param>
/// <param name="Primary">The button the card is, or null for a card whose environments are its buttons (Remote).</param>
/// <param name="Actions">The next actions of the state, after the card's own button.</param>
/// <param name="Rows">The environment buttons of a card that lists environments.</param>
public sealed record CodeLandingCard(
    CodeLandingCardKind Kind,
    CodeLandingCardState State,
    string Title,
    string Message,
    string OmpText,
    CodeLandingButton? Primary,
    IReadOnlyList<CodeLandingButton> Actions,
    IReadOnlyList<CodeLandingButton> Rows)
{
    /// <summary>Gets the text a screen reader announces for the card: its title, its state and its Omp status, each part ended by one full stop.</summary>
    public string AccessibleName => string.Join(" ", new[] { Title, Message, OmpText }
        .Where(static part => part.Length > 0)
        .Select(static part => part[^1] is '.' or '!' or '?' ? part : part + "."));
}
