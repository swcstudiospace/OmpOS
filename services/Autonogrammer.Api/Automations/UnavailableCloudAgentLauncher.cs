using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Automations;

/// <summary>The launcher of a host where cloud agents are not enabled. Every start fails with a sentence that says so.</summary>
internal sealed class UnavailableCloudAgentLauncher : ICloudAgentLauncher
{
    /// <inheritdoc />
    public Task<CloudLaunchResult> LaunchAsync(string ownerId, CreateCloudAgentRequest request, string prompt, string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(CloudLaunchResult.Failed("Cloud agents are not enabled on this API (Cloud:Enabled), so a cloud automation cannot start an agent."));
}
