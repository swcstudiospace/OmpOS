namespace OmpOS.Presentation.Modes;

/// <summary>
/// The seam where a pinned custom mode reaches the prompt. The code that sends a user's prompt to the engine (a turn start, a queued message that becomes a turn, a follow-up) passes the text through
/// <see cref="Apply"/> and sends the result. Slash commands the head runs itself are not prompts and do not pass through it.
/// </summary>
public interface IPromptPreamble
{
    /// <summary>Gets whether a mode is pinned. The head shows the mode chip while it is true.</summary>
    bool IsActive { get; }

    /// <summary>Puts the pinned mode's body in front of a prompt.</summary>
    /// <param name="prompt">The prompt the user wrote.</param>
    /// <returns>The prompt unchanged when no mode is pinned, otherwise the mode block followed by the prompt.</returns>
    string Apply(string prompt);
}
