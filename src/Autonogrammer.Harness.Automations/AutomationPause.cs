namespace Autonogrammer.Harness.Automations;

/// <summary>
/// A pause placed on an automation by a guard. It names the guard that paused the automation and says how to resume it.
/// </summary>
public sealed record AutomationPause
{
    /// <summary>
    /// Creates a pause.
    /// </summary>
    /// <param name="guard">The name of the guard that paused the automation.</param>
    /// <param name="reason">Why the guard paused it.</param>
    /// <param name="howToResume">What an operator does to resume the automation.</param>
    /// <exception cref="ArgumentException">A field is null, empty or white space.</exception>
    public AutomationPause(string guard, string reason, string howToResume)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guard);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(howToResume);
        Guard = guard;
        Reason = reason;
        HowToResume = howToResume;
    }

    /// <summary>The name of the guard that paused the automation.</summary>
    public string Guard { get; }

    /// <summary>Why the guard paused it.</summary>
    public string Reason { get; }

    /// <summary>What an operator does to resume the automation.</summary>
    public string HowToResume { get; }
}
