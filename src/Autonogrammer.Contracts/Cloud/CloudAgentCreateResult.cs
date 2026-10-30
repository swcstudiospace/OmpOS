namespace Autonogrammer.Contracts.Cloud;

/// <summary>The result of <see cref="ICloudAgentStore.CreateAsync"/>.</summary>
/// <param name="Outcome">How the call ended.</param>
/// <param name="Record">The stored record for <see cref="CloudAgentCreateOutcome.Created"/> and <see cref="CloudAgentCreateOutcome.Replayed"/>; otherwise null.</param>
public sealed record CloudAgentCreateResult(CloudAgentCreateOutcome Outcome, CloudAgentRecord? Record);
