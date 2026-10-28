namespace Autonogrammer.Native.Composition.Automations;

/// <summary>How the first turn of an automation's session ended.</summary>
/// <param name="Completed">True when the turn ended normally.</param>
/// <param name="Error">What went wrong when it did not, as one sentence without the prompt or any secret; null when it completed.</param>
internal sealed record AutomationSessionTurn(bool Completed, string? Error)
{
    /// <summary>Gets a turn that ended normally.</summary>
    public static AutomationSessionTurn Done { get; } = new(true, null);

    /// <summary>Creates a turn that failed.</summary>
    /// <param name="error">What went wrong.</param>
    /// <returns>The turn.</returns>
    public static AutomationSessionTurn Failed(string error) => new(false, error);
}
