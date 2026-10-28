namespace Autonogrammer.Native.Composition.Automations;

/// <summary>One agent session an automation run happens in. The target asks for the confirmation of a first run, sends the prompt and waits for the turn to end.</summary>
internal interface IAutomationSession
{
    /// <summary>Gets the identifier of the session, as the registry and the rail know it.</summary>
    string Id { get; }

    /// <summary>
    /// Asks the user to confirm that this automation may start agent sessions on its own, through the approval card of the session. The run waits for the answer.
    /// </summary>
    /// <param name="request">What the card says.</param>
    /// <param name="cancellationToken">Cancels the wait, which leaves the card unanswered.</param>
    /// <returns>True when the user allowed it; false when the user denied it or the session was closed.</returns>
    Task<bool> ConfirmAsync(AutomationConfirmation request, CancellationToken cancellationToken);

    /// <summary>Sends the prompt as the first turn of the session and waits until the turn ends.</summary>
    /// <param name="prompt">The prompt.</param>
    /// <param name="cancellationToken">Cancels the wait; the turn is then stopped.</param>
    /// <returns>How the turn ended.</returns>
    Task<AutomationSessionTurn> RunAsync(string prompt, CancellationToken cancellationToken);

    /// <summary>Closes the session, which stops its engine. The session stays in the rail with its transcript.</summary>
    /// <returns>A task that completes when the session is closed.</returns>
    Task CloseAsync();
}
