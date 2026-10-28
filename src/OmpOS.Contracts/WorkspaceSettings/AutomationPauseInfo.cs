namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The pause a guard placed on an automation.</summary>
public sealed record AutomationPauseInfo
{
    /// <summary>Gets the name of the guard that paused the automation.</summary>
    public required string Guard { get; init; }

    /// <summary>Gets why the guard paused it.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets what an operator does to resume the automation.</summary>
    public required string HowToResume { get; init; }
}
