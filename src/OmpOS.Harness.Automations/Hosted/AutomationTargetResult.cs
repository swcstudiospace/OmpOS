namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// How an <see cref="IAutomationTarget"/> ended a start.
/// </summary>
public sealed record AutomationTargetResult
{
    /// <summary>Gets how the run ended: <see cref="AutomationRunStatus.Succeeded"/>, <see cref="AutomationRunStatus.Launched"/> or <see cref="AutomationRunStatus.Failed"/>.</summary>
    public required AutomationRunStatus Status { get; init; }

    /// <summary>Gets the identifier of what the target created, for example the cloud agent, or null.</summary>
    public string? ExternalId { get; init; }

    /// <summary>Gets the failure text for a failed result. It never holds a secret, a prompt or a payload.</summary>
    public string? Error { get; init; }

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The failure text.</param>
    /// <returns>The result.</returns>
    public static AutomationTargetResult Failed(string error) => new() { Status = AutomationRunStatus.Failed, Error = error };

    /// <summary>Creates a launched result: the target took the work and continues on its own.</summary>
    /// <param name="externalId">What the target created.</param>
    /// <returns>The result.</returns>
    public static AutomationTargetResult Launched(string externalId) => new() { Status = AutomationRunStatus.Launched, ExternalId = externalId };

    /// <summary>Creates a succeeded result.</summary>
    /// <param name="externalId">What the target created, or null.</param>
    /// <returns>The result.</returns>
    public static AutomationTargetResult Succeeded(string? externalId = null) => new() { Status = AutomationRunStatus.Succeeded, ExternalId = externalId };
}
