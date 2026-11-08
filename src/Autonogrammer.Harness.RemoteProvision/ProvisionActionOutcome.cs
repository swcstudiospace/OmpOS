namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>What happened to one confirmed install.</summary>
/// <param name="Summary">The action's line, exactly as the user confirmed it.</param>
/// <param name="Status">Whether it succeeded, failed, or was not run because an earlier action failed.</param>
/// <param name="Message">For a failed action, the first 200 characters of what the script or the connection reported; otherwise null.</param>
public sealed record ProvisionActionOutcome(string Summary, ProvisionActionStatus Status, string? Message);
