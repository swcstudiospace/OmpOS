namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>One install the plan would run. Its <see cref="Summary"/> is the line the user confirms; the typed fields build its script.</summary>
/// <param name="Summary">The line shown to the user and recorded in the confirmation.</param>
internal abstract record ProvisionAction(string Summary);
