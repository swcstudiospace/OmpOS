using System.Globalization;
using Microsoft.Extensions.Options;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// Starts cloud agents through <see cref="CloudAgentService"/>, the same service the <c>/cloud/agents</c> routes use, so every cloud rule
/// applies to an automation's agent: owner caps, secret resolution, repository checks and idempotency. It creates the agent, polls it
/// until its worker can take a prompt, then sends the prompt. The run does not wait for the agent to finish.
/// </summary>
internal sealed class CloudServiceAgentLauncher : ICloudAgentLauncher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly CloudAgentService _agents;
    private readonly IOptions<ApiOptions> _api;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="CloudServiceAgentLauncher"/> class.</summary>
    /// <param name="agents">The cloud agent service.</param>
    /// <param name="api">The API options, for the prompt length limit.</param>
    /// <param name="clock">The clock that spaces the polls.</param>
    public CloudServiceAgentLauncher(CloudAgentService agents, IOptions<ApiOptions> api, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(clock);
        _agents = agents;
        _api = api;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<CloudLaunchResult> LaunchAsync(string ownerId, CreateCloudAgentRequest request, string prompt, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (prompt.Length > _api.Value.MaxPromptLength)
        {
            return CloudLaunchResult.Failed($"The run's prompt is longer than the API limit of {_api.Value.MaxPromptLength.ToString(CultureInfo.InvariantCulture)} characters.");
        }

        var created = await _agents.CreateAsync(ownerId, request, idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (created.Problem is not null)
        {
            return CloudLaunchResult.Failed($"The cloud agent was refused ({created.Problem.Code}).");
        }

        var agentId = created.Value!.Agent.Id;
        var ready = await WaitUntilReadyAsync(ownerId, agentId, cancellationToken).ConfigureAwait(false);
        if (ready is not null)
        {
            return CloudLaunchResult.Failed(ready);
        }

        var prompted = await _agents.PromptAsync(ownerId, agentId, prompt, idempotencyKey, null, cancellationToken).ConfigureAwait(false);
        return prompted.Problem is not null
            ? CloudLaunchResult.Failed($"The cloud agent {agentId} refused the prompt ({prompted.Problem.Code}).")
            : CloudLaunchResult.Started(agentId);
    }

    private async Task<string?> WaitUntilReadyAsync(string ownerId, string agentId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var read = await _agents.GetAsync(ownerId, agentId, cancellationToken).ConfigureAwait(false);
            if (read.Problem is not null)
            {
                return $"The cloud agent {agentId} could not be read ({read.Problem.Code}).";
            }

            var status = read.Value!.Status;
            if (status is CloudAgentStatus.Running or CloudAgentStatus.Idle)
            {
                return null;
            }

            if (status is not (CloudAgentStatus.Provisioning or CloudAgentStatus.Starting))
            {
                return $"The cloud agent {agentId} did not start (status {status}).";
            }

            await Task.Delay(PollInterval, _clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
