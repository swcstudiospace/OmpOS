namespace OmpOS.Presentation.Automations;

/// <summary>
/// The pause a guard placed on an automation, as an <see cref="IAutomationsSource"/> reports it: the guard that paused it, why, and
/// what an operator does to resume it. A head adapts the harness pause to this record, so the presentation layer stays free of the harness.
/// </summary>
/// <param name="Guard">The name of the guard that paused the automation.</param>
/// <param name="Reason">Why the guard paused it.</param>
/// <param name="HowToResume">What an operator does to resume the automation.</param>
public sealed record AutomationPauseEntry(string Guard, string Reason, string HowToResume);
