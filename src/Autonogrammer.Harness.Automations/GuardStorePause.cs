namespace Autonogrammer.Harness.Automations;

/// <summary>
/// The on-disk shape of an <see cref="AutomationPause"/>. It carries no secrets: a guard name, a reason and a resume hint.
/// </summary>
internal sealed class GuardStorePause
{
    /// <summary>The name of the guard that paused the automation.</summary>
    public string? Guard { get; set; }

    /// <summary>Why the guard paused it.</summary>
    public string? Reason { get; set; }

    /// <summary>What an operator does to resume the automation.</summary>
    public string? HowToResume { get; set; }
}
