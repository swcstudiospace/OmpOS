namespace Autonogrammer.Presentation.Cloud;

/// <summary>
/// What the Cloud agents page and the new-agent dialog need from the head: the list of agents, the commands on them and a signal that
/// something changed. A head maps its control-plane calls onto this interface; the presentation never sees a wire type, a token or a worker
/// address. Every expected failure is a <see cref="CloudHostException"/> with a plain message; an implementation must not let a transport
/// exception escape.
/// </summary>
public interface ICloudAgentsHost
{
    /// <summary>Occurs when the host learns that an agent or a limit changed. It may be raised on any thread; the models dispatch it.</summary>
    event EventHandler? Changes;

    /// <summary>Reads whether cloud agents are set up and what a new agent can choose. A host that is not configured returns <see cref="CloudSetup.NotConfigured"/> instead of throwing.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The setup.</returns>
    /// <exception cref="CloudHostException">The service cannot be reached.</exception>
    Task<CloudSetup> GetSetupAsync(CancellationToken cancellationToken);

    /// <summary>Reads how many agents and awake workers exist and the caps.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The limits.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task<CloudLimits> GetLimitsAsync(CancellationToken cancellationToken);

    /// <summary>Lists the user's cloud agents.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Every agent, in any order.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task<IReadOnlyList<CloudAgentRecord>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates a cloud agent. This starts billable compute, so a caller asks the user to confirm the cost first.</summary>
    /// <param name="request">What to create.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new agent.</returns>
    /// <exception cref="CloudHostException">The request was refused (<see cref="CloudFailureKind.LimitReached"/>, <see cref="CloudFailureKind.QuotaExceeded"/>, <see cref="CloudFailureKind.Rejected"/>) or failed.</exception>
    Task<CloudAgentRecord> CreateAsync(CloudAgentRequest request, CancellationToken cancellationToken);

    /// <summary>Wakes a stopped agent.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent after the request.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task<CloudAgentRecord> WakeAsync(string agentId, CancellationToken cancellationToken);

    /// <summary>Puts a running or idle agent's worker to sleep.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The agent after the request.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task<CloudAgentRecord> StopAsync(string agentId, CancellationToken cancellationToken);

    /// <summary>Deletes an agent and its worker. The caller asks the user to confirm first.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the deletion.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task DeleteAsync(string agentId, CancellationToken cancellationToken);

    /// <summary>Sends a message to an agent.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the service accepted the message.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task PromptAsync(string agentId, string text, CancellationToken cancellationToken);

    /// <summary>Pushes the agent's branch and opens a pull request for one repository. The pull request is a draft unless the request says otherwise.</summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="request">The pull request to open.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pull request that was opened.</returns>
    /// <exception cref="CloudHostException">The call failed.</exception>
    Task<CloudPullRequestLink> CreatePullRequestAsync(string agentId, CloudPullRequestRequest request, CancellationToken cancellationToken);

    /// <summary>Shows a pull request page to the user, in the system browser.</summary>
    /// <param name="link">The pull request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the page was handed to the browser.</returns>
    /// <exception cref="CloudHostException">The page could not be opened.</exception>
    Task OpenPullRequestAsync(CloudPullRequestLink link, CancellationToken cancellationToken);
}
