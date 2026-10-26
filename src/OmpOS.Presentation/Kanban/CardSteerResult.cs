namespace OmpOS.Presentation.Kanban;

/// <summary>The truthful result of one steering attempt from a card, with a plain message the card shows and announces.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Message">The plain message. For a failure it says why, for example that the session ended.</param>
public sealed record CardSteerResult(CardSteerOutcome Outcome, string Message)
{
    /// <summary>Gets whether the message was accepted, delivered or queued.</summary>
    public bool IsAccepted => Outcome != CardSteerOutcome.Failed;

    /// <summary>Creates a result for a message the session took at once.</summary>
    /// <param name="message">The plain message, or null for the default.</param>
    /// <returns>The result.</returns>
    public static CardSteerResult Delivered(string? message = null) => new(CardSteerOutcome.Delivered, message ?? "Sent to the session");

    /// <summary>Creates a result for a message that waits for the end of the turn.</summary>
    /// <param name="message">The plain message, or null for the default.</param>
    /// <returns>The result.</returns>
    public static CardSteerResult Queued(string? message = null) => new(CardSteerOutcome.Queued, message ?? "Queued for the next turn");

    /// <summary>Creates a result for a message that was refused.</summary>
    /// <param name="message">The plain reason.</param>
    /// <returns>The result.</returns>
    public static CardSteerResult Failed(string message) => new(CardSteerOutcome.Failed, message);
}
