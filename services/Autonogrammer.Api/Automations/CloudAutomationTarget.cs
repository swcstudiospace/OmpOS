using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// The cloud target of automation runs: it turns the automation's target into a <see cref="CreateCloudAgentRequest"/> and has the
/// <see cref="ICloudAgentLauncher"/> start the agent and hand it the prompt. The run ends as <see cref="AutomationRunStatus.Launched"/>:
/// the agent continues on its own and is followed through <c>/cloud/agents/{id}</c>. The allowed tools travel in the prompt; the cloud
/// agent's own pull request service enforces who may open a pull request.
/// </summary>
internal sealed class CloudAutomationTarget : IAutomationTarget
{
    private const int MaxAgentNameLength = 100;

    private readonly ICloudAgentLauncher _launcher;

    /// <summary>Initializes a new instance of the <see cref="CloudAutomationTarget"/> class.</summary>
    /// <param name="launcher">The launcher that starts agents.</param>
    public CloudAutomationTarget(ICloudAgentLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
    }

    /// <inheritdoc />
    public AutomationTargetKind Kind => AutomationTargetKind.Cloud;

    /// <inheritdoc />
    public async Task<AutomationTargetResult> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = request.Automation.Target;
        var agent = new CreateCloudAgentRequest
        {
            Name = Truncate(request.Automation.Name),
            Repositories = target.Repositories
                .Select(static repository => new CloudRepository { Address = repository.Address, Branch = repository.Branch, BaseBranch = repository.BaseBranch })
                .ToArray(),
            SecretRefs = target.SecretRefs.Count == 0 ? null : target.SecretRefs,
            EnvironmentId = target.EnvironmentId,
            Model = target.Model,
            Mode = target.Mode,
        };
        var launched = await _launcher.LaunchAsync(request.Automation.Owner, agent, request.Prompt, request.RunId, cancellationToken).ConfigureAwait(false);
        return launched.Succeeded
            ? AutomationTargetResult.Launched(launched.AgentId!)
            : AutomationTargetResult.Failed(launched.Error ?? "The cloud agent could not be started.");
    }

    private static string Truncate(string name) => name.Length <= MaxAgentNameLength ? name : name[..MaxAgentNameLength];
}
