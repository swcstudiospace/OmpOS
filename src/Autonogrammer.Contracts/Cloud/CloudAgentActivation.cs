namespace Autonogrammer.Contracts.Cloud;

/// <summary>The result of <see cref="ICloudAgentStore.TryActivateAsync"/>.</summary>
/// <param name="Outcome">How the call ended.</param>
/// <param name="Record">The record after the call, or null for <see cref="CloudAgentActivationOutcome.NotFound"/>.</param>
public sealed record CloudAgentActivation(CloudAgentActivationOutcome Outcome, CloudAgentRecord? Record);
