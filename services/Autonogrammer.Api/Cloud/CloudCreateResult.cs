using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>The result of creating an agent.</summary>
/// <param name="Agent">The agent.</param>
/// <param name="Replayed">True when the Idempotency-Key matched an earlier identical request and the earlier agent is returned.</param>
internal sealed record CloudCreateResult(CloudAgentResponse Agent, bool Replayed);
