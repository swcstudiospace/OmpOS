using Autonogrammer.Harness.Automations.Hosted;
using Autonogrammer.Presentation.Cloud;

namespace Autonogrammer.Native.Composition.Automations;

/// <summary>
/// Runs an automation on a cloud agent through the cloud agents host of the head (the Autonogrammer API): the agent is created with the automation's repositories, environment, secret names and model, and
/// the prompt is sent to it as its first task. The run is launched once the prompt is accepted; the agent's own progress is the cloud agents page's. The repository branches of the first repository apply
/// to every repository of the agent, because the host's request has one branch and one base branch.
/// </summary>
internal sealed class CloudHostAutomationTarget : IAutomationTarget
{
    private const int MaxAgentNameLength = 100;

    private readonly ICloudAgentsHost _host;

    /// <summary>Initializes a new instance of the <see cref="CloudHostAutomationTarget"/> class.</summary>
    /// <param name="host">The cloud agents host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public CloudHostAutomationTarget(ICloudAgentsHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <inheritdoc/>
    public AutomationTargetKind Kind => AutomationTargetKind.Cloud;

    /// <inheritdoc/>
    public async Task<AutomationTargetResult> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var automation = request.Automation;
        var target = automation.Target;
        var first = target.Repositories.Count > 0 ? target.Repositories[0] : null;
        var agent = await _host.CreateAsync(
            new CloudAgentRequest
            {
                Name = automation.Name.Length <= MaxAgentNameLength ? automation.Name : automation.Name[..MaxAgentNameLength],
                Repositories = [.. target.Repositories.Select(static repository => new CloudRepository(repository.Address))],
                Branch = first?.Branch,
                BaseBranch = first?.BaseBranch,
                EnvironmentId = target.EnvironmentId,
                SecretNames = target.SecretRefs,
                Model = target.Model,
            },
            cancellationToken).ConfigureAwait(false);
        try
        {
            await _host.PromptAsync(agent.Id, request.Prompt, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudHostException)
        {
            // The agent exists and idles out on its own; the run says which one it is so the user can open or delete it.
            return new AutomationTargetResult
            {
                Status = AutomationRunStatus.Failed,
                ExternalId = agent.Id,
                Error = "The cloud agent was created but its first task could not be sent.",
            };
        }

        return AutomationTargetResult.Launched(agent.Id);
    }
}
