using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Automations;

/// <summary>
/// Starts a cloud agent for an automation run: creates the agent, waits until its worker is ready and sends it the run's prompt. The
/// default implementation drives <c>CloudAgentService</c>; a host or a test may register another one.
/// </summary>
internal interface ICloudAgentLauncher
{
    /// <summary>Starts an agent and gives it the prompt.</summary>
    /// <param name="ownerId">The owner of the automation; the agent belongs to the same owner.</param>
    /// <param name="request">The agent to create.</param>
    /// <param name="prompt">The complete prompt.</param>
    /// <param name="idempotencyKey">The run identifier. A repeated call with the same key never creates a second agent.</param>
    /// <param name="cancellationToken">Cancelled when the run exceeds its maximum runtime or the host stops.</param>
    /// <returns>The identifier of the agent, or the reason it could not be started.</returns>
    Task<CloudLaunchResult> LaunchAsync(string ownerId, CreateCloudAgentRequest request, string prompt, string idempotencyKey, CancellationToken cancellationToken);
}
