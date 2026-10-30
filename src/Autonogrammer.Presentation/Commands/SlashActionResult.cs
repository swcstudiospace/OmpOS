namespace Autonogrammer.Presentation.Commands;

/// <summary>The result of running a native action.</summary>
/// <param name="Outcome">How the action ended.</param>
/// <param name="Message">A sentence for the composer's status line, or an empty string when there is nothing to say.</param>
/// <param name="FollowUpPrompt">Text to send to the engine as an ordinary prompt after the action (for example the text typed after <c>/plan</c>), or <see langword="null"/>.</param>
public sealed record SlashActionResult(SlashActionOutcome Outcome, string Message, string? FollowUpPrompt = null)
{
    /// <summary>Creates a completed result.</summary>
    /// <param name="message">The status sentence, or an empty string.</param>
    /// <param name="followUpPrompt">The prompt to send next, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public static SlashActionResult Completed(string message = "", string? followUpPrompt = null) => new(SlashActionOutcome.Completed, message, followUpPrompt);

    /// <summary>Creates a failed result.</summary>
    /// <param name="message">Why the action failed.</param>
    /// <returns>The result.</returns>
    public static SlashActionResult Failed(string message) => new(SlashActionOutcome.Failed, message);

    /// <summary>Creates a not-available result.</summary>
    /// <param name="message">Why the head cannot run the action.</param>
    /// <returns>The result.</returns>
    public static SlashActionResult NotAvailable(string message) => new(SlashActionOutcome.NotAvailable, message);
}
