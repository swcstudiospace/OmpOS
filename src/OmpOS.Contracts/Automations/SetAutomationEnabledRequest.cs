namespace OmpOS.Contracts.Automations;

/// <summary>The body of <c>PUT /automations/{id}/enabled</c>.</summary>
public sealed record SetAutomationEnabledRequest
{
    /// <summary>Gets a value indicating whether triggers start runs.</summary>
    public required bool Enabled { get; init; }
}
