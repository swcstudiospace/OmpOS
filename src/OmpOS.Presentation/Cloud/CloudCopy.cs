using System.Globalization;

namespace OmpOS.Presentation.Cloud;

/// <summary>
/// Every text of the Cloud agents page, the new-agent dialog and the cloud hand-off, in one place so the views carry no literal. No
/// sentence holds a token, a secret value, a worker address or a path; a sentence about an entry never repeats what the user typed.
/// </summary>
public static class CloudCopy
{
    // Page.

    /// <summary>The navigation label of the Cloud agents page.</summary>
    public const string Title = "Cloud agents";

    /// <summary>The heading of the Cloud agents page.</summary>
    public const string Heading = "Cloud agents";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "Agents that run on a cloud worker, keep working while this computer is off, and open pull requests from their own branch.";

    /// <summary>The label of the button that opens the new-agent dialog.</summary>
    public const string NewAgentLabel = "New cloud agent";

    /// <summary>The label of the button that reads the list again.</summary>
    public const string RefreshLabel = "Refresh";

    /// <summary>The label of the button that repeats a failed load.</summary>
    public const string TryAgainLabel = "Try again";

    /// <summary>The label of the button that checks the setup again.</summary>
    public const string CheckAgainLabel = "Check again";

    /// <summary>The text shown while the list loads.</summary>
    public const string Loading = "Loading cloud agents.";

    /// <summary>The label of the status filter.</summary>
    public const string FilterLabel = "Show";

    /// <summary>The accessible name of the status filter.</summary>
    public const string FilterName = "Filter cloud agents by status";

    /// <summary>The entry of the status filter that keeps every status.</summary>
    public const string AllStatuses = "All statuses";

    /// <summary>The accessible name of the list of agents.</summary>
    public const string ListName = "Cloud agents";

    /// <summary>The accessible name of the usage line.</summary>
    public const string LimitsName = "Cloud agent usage";

    /// <summary>The heading of the not-set-up state.</summary>
    public const string NotConfiguredHeading = "Cloud agents are not set up";

    /// <summary>The sentence of the not-set-up state, and the fallback reason.</summary>
    public const string NotConfiguredIntro = "Cloud agents need an OmpOS service that can start workers. Nothing is billed until you create an agent.";

    /// <summary>The sentence in front of the setup steps.</summary>
    public const string SetupStepsIntro = "To set them up:";

    /// <summary>The accessible name of the list of setup steps.</summary>
    public const string SetupStepsName = "Setup steps";

    /// <summary>The short sentence for a failed call because cloud agents are not set up.</summary>
    public const string NotConfiguredShort = "Cloud agents are not set up.";

    /// <summary>The sentence for a call the service refused because the user is not signed in.</summary>
    public const string SignInNeeded = "Sign in to the OmpOS service, then try again.";

    /// <summary>The heading of the failed-load state.</summary>
    public const string LoadFailedHeading = "Cloud agents could not be loaded";

    /// <summary>The body of the failed-load state when the host gave no reason.</summary>
    public const string LoadFailed = "The cloud agents could not be loaded. Check your connection and try again.";

    /// <summary>The body of the failed-load state after a cancelled load.</summary>
    public const string LoadCancelled = "Loading was cancelled.";

    /// <summary>The sentence of a refresh that failed while an older list is still shown.</summary>
    public const string RefreshFailed = "The list could not be refreshed. It shows the last list that loaded.";

    /// <summary>The accessible name of the refresh failure message.</summary>
    public const string RefreshErrorName = "Refresh failed";

    /// <summary>The heading of the state with no agent.</summary>
    public const string EmptyHeading = "No cloud agents yet";

    /// <summary>The body of the state with no agent.</summary>
    public const string EmptyBody = "Create one to hand a task to a cloud worker. It works on its own branch and can open a pull request.";

    /// <summary>The name an agent without a name is shown with.</summary>
    public const string UntitledAgent = "Untitled cloud agent";

    /// <summary>The words for an agent that has no activity yet.</summary>
    public const string NoActivity = "No activity yet";

    /// <summary>The words for an agent without a repository.</summary>
    public const string NoRepositories = "No repository";

    /// <summary>The sentence of the details area when no agent is selected.</summary>
    public const string NoSelection = "Select a cloud agent to see its details.";

    // Details.

    /// <summary>The heading of the details area.</summary>
    public const string DetailsHeading = "Details";

    /// <summary>The label of the repositories list of the details.</summary>
    public const string RepositoriesLabel = "Repositories";

    /// <summary>The label of the pull requests list of the details.</summary>
    public const string PullRequestsLabel = "Pull requests";

    /// <summary>The label of the status history of the details.</summary>
    public const string HistoryLabel = "Status history";

    /// <summary>The sentence of an agent without a status history.</summary>
    public const string NoHistory = "No history yet.";

    /// <summary>The accessible name of the actions of the details.</summary>
    public const string ActionsName = "Cloud agent actions";

    /// <summary>The label of Wake.</summary>
    public const string WakeLabel = "Wake";

    /// <summary>The label of Stop.</summary>
    public const string StopLabel = "Stop";

    /// <summary>The label of Delete.</summary>
    public const string DeleteLabel = "Delete";

    /// <summary>The label of Open in Agents.</summary>
    public const string OpenInAgentsLabel = "Open in Agents";

    /// <summary>The label of the button that confirms a delete.</summary>
    public const string ConfirmDeleteLabel = "Delete agent";

    /// <summary>The label of the Cancel buttons.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The label of the buttons that remove a repository or a secret name from the new-agent form.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The label of the message box of the details.</summary>
    public const string PromptLabel = "Message to the agent";

    /// <summary>The label of Send.</summary>
    public const string SendLabel = "Send";

    /// <summary>The label of the button that opens an existing pull request.</summary>
    public const string OpenPullRequestLabel = "Open pull request";

    /// <summary>The label of the button that creates a pull request.</summary>
    public const string CreatePullRequestLabel = "Create pull request";

    /// <summary>The accessible name of the polite announcement of a finished command.</summary>
    public const string NoticeName = "Cloud agent update";

    /// <summary>The accessible name of the failure message of a command.</summary>
    public const string ActionErrorName = "Cloud agent action failed";

    /// <summary>The accessible name of the delete confirmation.</summary>
    public const string DeleteConfirmationName = "Delete confirmation";

    /// <summary>The accessible name of the message about the repository address entry.</summary>
    public const string RepositoryProblemName = "Repository address problem";

    /// <summary>The accessible name of the message about the secret name entry.</summary>
    public const string SecretProblemName = "Secret name problem";

    /// <summary>The sentence of a command that failed without a reason from the service.</summary>
    public const string ActionFailed = "That did not work. Try again.";

    /// <summary>The sentence for an agent the service no longer has.</summary>
    public const string AgentNotFound = "That agent no longer exists.";

    // Statuses.

    /// <summary>The words of <see cref="CloudAgentStatus.Provisioning"/>.</summary>
    public const string StatusProvisioning = "Provisioning";

    /// <summary>The words of <see cref="CloudAgentStatus.Starting"/>.</summary>
    public const string StatusStarting = "Starting";

    /// <summary>The words of <see cref="CloudAgentStatus.Running"/>.</summary>
    public const string StatusRunning = "Running";

    /// <summary>The words of <see cref="CloudAgentStatus.Idle"/>.</summary>
    public const string StatusIdle = "Idle";

    /// <summary>The words of <see cref="CloudAgentStatus.Stopping"/>.</summary>
    public const string StatusStopping = "Stopping";

    /// <summary>The words of <see cref="CloudAgentStatus.Stopped"/>.</summary>
    public const string StatusStopped = "Stopped";

    /// <summary>The words of <see cref="CloudAgentStatus.Failed"/>.</summary>
    public const string StatusFailed = "Failed";

    /// <summary>The words of <see cref="CloudAgentStatus.Deleting"/>.</summary>
    public const string StatusDeleting = "Deleting";

    /// <summary>The words of <see cref="CloudAgentStatus.Orphaned"/>.</summary>
    public const string StatusOrphaned = "Orphaned";

    /// <summary>The words of <see cref="CloudAgentStatus.LimitReached"/>.</summary>
    public const string StatusLimitReached = "Limit reached";

    /// <summary>The words of a status the model does not know.</summary>
    public const string StatusUnknown = "Status unknown";

    /// <summary>What <see cref="CloudAgentStatus.Provisioning"/> means.</summary>
    public const string DescribeProvisioning = "A worker is being set up and the repositories are being cloned. This can take a few minutes.";

    /// <summary>What <see cref="CloudAgentStatus.Starting"/> means.</summary>
    public const string DescribeStarting = "The worker is waking up. The agent will be reachable shortly.";

    /// <summary>What <see cref="CloudAgentStatus.Running"/> means.</summary>
    public const string DescribeRunning = "The agent is working on a task. The worker is awake and billing compute.";

    /// <summary>What <see cref="CloudAgentStatus.Idle"/> means.</summary>
    public const string DescribeIdle = "The worker is awake and waiting for a message. It goes to sleep after its idle time.";

    /// <summary>What <see cref="CloudAgentStatus.Stopping"/> means.</summary>
    public const string DescribeStopping = "The worker is going to sleep.";

    /// <summary>What <see cref="CloudAgentStatus.Stopped"/> means.</summary>
    public const string DescribeStopped = "The worker is asleep. Wake it to continue from its saved files; work that was running is not resumed.";

    /// <summary>What <see cref="CloudAgentStatus.Failed"/> means.</summary>
    public const string DescribeFailed = "The agent ended with an error. The status history may say why.";

    /// <summary>What <see cref="CloudAgentStatus.Deleting"/> means.</summary>
    public const string DescribeDeleting = "The worker and its saved files are being removed.";

    /// <summary>What <see cref="CloudAgentStatus.Orphaned"/> means.</summary>
    public const string DescribeOrphaned = "The service no longer knows this worker. Delete the agent to clean up.";

    /// <summary>What <see cref="CloudAgentStatus.LimitReached"/> means.</summary>
    public const string DescribeLimitReached = "A limit stopped this agent from continuing. Stop or delete another agent, or ask for a higher limit.";

    // New agent dialog.

    /// <summary>The title of the new-agent dialog.</summary>
    public const string NewAgentTitle = "New cloud agent";

    /// <summary>The sentence under the title of the new-agent dialog.</summary>
    public const string NewAgentDescription = "The agent runs on a cloud worker with a copy of your repositories and its own branch.";

    /// <summary>The label of the name field.</summary>
    public const string NameLabel = "Name";

    /// <summary>The label of the repository address field.</summary>
    public const string RepositoryEntryLabel = "Repository address";

    /// <summary>The help of the repository address field.</summary>
    public const string RepositoryEntryHelp = "An https address, for example https://github.com/owner/repository. Never put a user name or token in it.";

    /// <summary>The label of the button that adds a repository.</summary>
    public const string AddRepositoryLabel = "Add repository";

    /// <summary>The accessible name of the list of added repositories.</summary>
    public const string RepositoryListName = "Repositories of the agent";

    /// <summary>The sentence shown while no repository is added.</summary>
    public const string NoRepositoriesAdded = "No repository added yet. The first one is the primary repository.";

    /// <summary>The label of the branch field.</summary>
    public const string BranchLabel = "Agent branch";

    /// <summary>The help of the branch field.</summary>
    public const string BranchHelp = "The branch the agent commits to. Leave it empty and the service names one. The agent never pushes to the default branch.";

    /// <summary>The label of the base branch field.</summary>
    public const string BaseBranchLabel = "Start from branch";

    /// <summary>The help of the base branch field.</summary>
    public const string BaseBranchHelp = "Leave it empty to start from each repository's default branch.";

    /// <summary>The label of the environment field.</summary>
    public const string EnvironmentLabel = "Environment";

    /// <summary>The label of the model field.</summary>
    public const string ModelLabel = "Model";

    /// <summary>The entry of the model field that lets the service choose.</summary>
    public const string DefaultModelLabel = "Service default";

    /// <summary>The label of the secrets block.</summary>
    public const string SecretsLabel = "Secrets";

    /// <summary>The help of the secrets block.</summary>
    public const string SecretsHelp = "Names only. The values stay with the service; they are never typed or shown here.";

    /// <summary>The label of the secret name field.</summary>
    public const string SecretEntryLabel = "Secret name";

    /// <summary>The label of the button that adds a secret name.</summary>
    public const string AddSecretLabel = "Add secret name";

    /// <summary>The accessible name of the list of added secret names.</summary>
    public const string SecretListName = "Secret names of the agent";

    /// <summary>The sentence shown while no secret name is added.</summary>
    public const string NoSecretsAdded = "No secret names added.";

    /// <summary>The label of the pick list of stored secret names.</summary>
    public const string KnownSecretLabel = "Stored secrets";

    /// <summary>The label of the button that adds the picked stored secret.</summary>
    public const string AddKnownSecretLabel = "Add stored secret";

    /// <summary>The label of the idle time field.</summary>
    public const string IdleLabel = "Sleep after (minutes)";

    /// <summary>The help of the idle time field.</summary>
    public const string IdleHelp = "How long the worker stays awake without activity before it sleeps. A longer time costs more.";

    /// <summary>The label of the first task field.</summary>
    public const string FirstTaskLabel = "First task (optional)";

    /// <summary>The heading of the cost confirmation.</summary>
    public const string CostHeading = "Cost";

    /// <summary>The label of the checkbox that confirms the cost.</summary>
    public const string CostConfirmLabel = "I understand this agent uses billable compute while its worker is awake.";

    /// <summary>The label of the Create button.</summary>
    public const string CreateLabel = "Create agent";

    /// <summary>The text of the Create button while a create runs.</summary>
    public const string CreatingLabel = "Creating";

    /// <summary>The accessible name of the result of a create.</summary>
    public const string ResultName = "Create result";

    /// <summary>The accessible name of the reason Create is disabled.</summary>
    public const string WhyDisabledName = "Why Create is unavailable";

    /// <summary>The sentence that says what is missing when the cost is not confirmed.</summary>
    public const string CostNotConfirmed = "Tick the box to confirm the cost before you create the agent.";

    /// <summary>The sentence of an attempt before the form is complete.</summary>
    public const string CreateNotReady = "Complete the form before you create the agent.";

    /// <summary>The sentence while a create runs.</summary>
    public const string CreateInProgress = "The agent is being created.";

    /// <summary>The sentence of a cancelled create.</summary>
    public const string CreateCancelled = "Creating the agent was cancelled. Nothing was changed on your side; check the list before you try again.";

    /// <summary>The sentence of a failed create.</summary>
    public const string CreateFailed = "The agent could not be created. Check your connection and try again.";

    /// <summary>The sentence of a create the service refused.</summary>
    public const string CreateRejected = "The service did not accept this agent. Change an entry and try again.";

    /// <summary>The sentence of a create refused because a cap is reached.</summary>
    public const string LimitReached = "A limit was reached, so no more agents can be started. Stop or delete an agent first.";

    /// <summary>The sentence of a create refused because the quota is used up.</summary>
    public const string QuotaExceeded = "The compute quota is used up. Wait until it renews or ask for a higher quota.";

    /// <summary>The sentence of a hand-off without a prompt.</summary>
    public const string HandoffNeedsPrompt = "Write the task for the cloud agent first.";

    // Entry errors.

    /// <summary>A name is missing.</summary>
    public const string NameRequired = "Enter a name for the agent.";

    /// <summary>A name has a control character.</summary>
    public const string NameInvalid = "The name cannot contain control characters.";

    /// <summary>A repository address is missing.</summary>
    public const string RepositoryRequired = "Enter a repository address.";

    /// <summary>A repository address is too long.</summary>
    public const string RepositoryTooLong = "The address is too long.";

    /// <summary>A repository address has spaces or control characters.</summary>
    public const string RepositoryInvalid = "That address cannot be used. Remove spaces and special characters.";

    /// <summary>A repository address is not https.</summary>
    public const string RepositoryNotHttps = "Use an https address, for example https://github.com/owner/repository.";

    /// <summary>A repository address has credentials.</summary>
    public const string RepositoryHasCredentials = "Remove the user name or token from the address. Credentials never belong in a repository address.";

    /// <summary>A repository address has a query or a fragment.</summary>
    public const string RepositoryHasQuery = "Remove everything after the repository name, including any question mark or hash.";

    /// <summary>A repository address has no owner or no repository name.</summary>
    public const string RepositoryNeedsOwnerAndName = "The address needs an owner and a repository name, for example https://github.com/owner/repository.";

    /// <summary>A repository is already added.</summary>
    public const string RepositoryDuplicate = "That repository is already in the list.";

    /// <summary>No repository is added.</summary>
    public const string RepositoryNeeded = "Add at least one repository.";

    /// <summary>A branch name is not valid.</summary>
    public const string BranchInvalid = "Use a valid git branch name: no spaces, no two dots in a row, and none of ~ ^ : ? * [ or a backslash.";

    /// <summary>A secret name is missing.</summary>
    public const string SecretNameRequired = "Enter the name of a stored secret.";

    /// <summary>A secret name is not valid. The sentence never repeats the entry.</summary>
    public const string SecretNameInvalid = "A secret name has letters, digits and underscores only and does not start with a digit. Enter the name only, never the secret's value.";

    /// <summary>A secret name is already added.</summary>
    public const string SecretNameDuplicate = "That secret name is already in the list.";

    // Formatted copy.

    /// <summary>Gets the list of setup steps shown when cloud agents are not set up.</summary>
    public static IReadOnlyList<string> SetupSteps { get; } =
    [
        "Run the OmpOS service with cloud agents turned on. It starts and stops the workers.",
        "Connect a Railway account to the service, so it can create workers there. Railway bills the compute the workers use.",
        "Connect GitHub to the service, so workers can clone your repositories and push a branch of their own. They never push to the default branch.",
        "Sign in to the service from this app, then choose Check again.",
    ];

    /// <summary>Gets a setup step with its number, for example <c>1. Run the OmpOS service</c>.</summary>
    /// <param name="number">The 1-based number of the step.</param>
    /// <param name="step">The step text from <see cref="SetupSteps"/>.</param>
    /// <returns>The numbered line.</returns>
    public static string SetupStepLine(int number, string step) => string.Create(CultureInfo.InvariantCulture, $"{number}. {step}");

    /// <summary>Gets the cost confirmation: what is billed and when an idle worker sleeps.</summary>
    /// <param name="idleMinutes">The idle time in minutes.</param>
    /// <returns>The sentence.</returns>
    public static string CostNotice(int idleMinutes) =>
        string.Create(CultureInfo.InvariantCulture, $"Railway bills compute while the worker is awake; idle workers sleep after {Minutes(idleMinutes)}.");

    /// <summary>Gets the accessible name of a list row.</summary>
    /// <param name="name">The agent name.</param>
    /// <param name="status">The status words.</param>
    /// <param name="lastActivity">The relative time of the last activity.</param>
    /// <returns>The accessible name.</returns>
    public static string RowAccessibleName(string name, string status, string lastActivity) => $"{name}, cloud agent, {status}, {lastActivity}";

    /// <summary>Gets the detail line of a row: the repositories and the branch.</summary>
    /// <param name="repositories">The repository names.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The line.</returns>
    public static string RepositoriesOnBranch(string repositories, string branch) => $"{repositories} on {branch}";

    /// <summary>Gets the usage line of the page.</summary>
    /// <param name="limits">The counts and caps.</param>
    /// <returns>The line.</returns>
    public static string LimitsLine(CloudLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var agents = limits.MaxAgents is { } maxAgents
            ? string.Create(CultureInfo.InvariantCulture, $"{limits.Agents} of {maxAgents} {Plural(maxAgents, "agent", "agents")}")
            : string.Create(CultureInfo.InvariantCulture, $"{limits.Agents} {Plural(limits.Agents, "agent", "agents")}");
        var awake = limits.MaxAwakeAgents is { } maxAwake
            ? string.Create(CultureInfo.InvariantCulture, $"{limits.AwakeAgents} of {maxAwake} awake")
            : string.Create(CultureInfo.InvariantCulture, $"{limits.AwakeAgents} awake");
        return $"{agents}, {awake}";
    }

    /// <summary>Gets the sentence of a status filter that matches nothing.</summary>
    /// <param name="status">The status words.</param>
    /// <returns>The sentence.</returns>
    public static string NoMatches(string status) => $"No cloud agent has the status {status}.";

    /// <summary>Gets the branch line of the details.</summary>
    /// <param name="branch">The branch, or an empty string.</param>
    /// <returns>The line.</returns>
    public static string BranchLine(string branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return branch.Length == 0 ? "Branch: not created yet" : $"Branch: {branch}";
    }

    /// <summary>Gets the base branch line of the details.</summary>
    /// <param name="branch">The base branch, or an empty string.</param>
    /// <returns>The line.</returns>
    public static string BaseBranchLine(string branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return branch.Length == 0 ? "Starts from the default branch" : $"Starts from {branch}";
    }

    /// <summary>Gets the environment line of the details.</summary>
    /// <param name="environment">The environment name, or an empty string.</param>
    /// <returns>The line.</returns>
    public static string EnvironmentLine(string environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.Length == 0 ? "Environment: default" : $"Environment: {environment}";
    }

    /// <summary>Gets the model line of the details.</summary>
    /// <param name="model">The model, or an empty string.</param>
    /// <returns>The line.</returns>
    public static string ModelLine(string model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Length == 0 ? "Model: service default" : $"Model: {model}";
    }

    /// <summary>Gets the idle line of the details.</summary>
    /// <param name="minutes">The idle time in minutes.</param>
    /// <returns>The line.</returns>
    public static string IdleLine(int minutes) => $"Sleeps after {Minutes(minutes)} without activity";

    /// <summary>Gets the last activity line of the details.</summary>
    /// <param name="relative">The relative time.</param>
    /// <returns>The line.</returns>
    public static string LastActivityLine(string relative) => $"Last activity: {relative}";

    /// <summary>Gets the accessible name of a history step.</summary>
    /// <param name="status">The status words.</param>
    /// <param name="time">The relative time.</param>
    /// <param name="note">The reason, or an empty string.</param>
    /// <returns>The accessible name.</returns>
    public static string TimelineAccessibleName(string status, string time, string note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return note.Length == 0 ? $"{status}, {time}" : $"{status}, {time}, {note}";
    }

    /// <summary>Gets the confirmation of a delete: what is removed and what stays.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The question.</returns>
    public static string DeleteWarning(string name) =>
        $"Delete {name}? Its worker and saved files are removed and cannot be brought back. Branches already pushed and pull requests stay on GitHub.";

    /// <summary>Gets the accessible name of Wake.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string WakeName(string name) => $"Wake {name}";

    /// <summary>Gets the accessible name of Stop.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string StopName(string name) => $"Stop {name}";

    /// <summary>Gets the accessible name of Delete.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string DeleteName(string name) => $"Delete {name}";

    /// <summary>Gets the accessible name of the button that confirms a delete.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string ConfirmDeleteName(string name) => $"Confirm deleting {name}";

    /// <summary>Gets the accessible name of Open in Agents.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string OpenInAgentsName(string name) => $"Open {name} in Agents";

    /// <summary>Gets the accessible name of the message box.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string PromptName(string name) => $"Message to {name}";

    /// <summary>Gets the accessible name of Send.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The accessible name.</returns>
    public static string SendName(string name) => $"Send message to {name}";

    /// <summary>Gets the announcement of a wake.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string WakeRequested(string name) => $"Waking {name}.";

    /// <summary>Gets the announcement of a stop.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string StopRequested(string name) => $"Stopping {name}.";

    /// <summary>Gets the announcement of a delete.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string DeleteRequested(string name) => $"{name} is being deleted.";

    /// <summary>Gets the announcement of a message that was sent.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string PromptSent(string name) => $"Message sent to {name}.";

    /// <summary>Gets the announcement of a pull request that was created.</summary>
    /// <param name="repository">The repository name.</param>
    /// <returns>The sentence.</returns>
    public static string PullRequestCreated(string repository) => $"Draft pull request created for {repository}.";

    /// <summary>Gets the announcement of a pull request that was opened in the browser.</summary>
    /// <param name="repository">The repository name.</param>
    /// <returns>The sentence.</returns>
    public static string PullRequestOpened(string repository) => $"Opened the pull request of {repository}.";

    /// <summary>Gets the accessible name of the button that opens a pull request.</summary>
    /// <param name="repository">The repository name.</param>
    /// <returns>The accessible name.</returns>
    public static string OpenPullRequestName(string repository) => $"Open the pull request of {repository}";

    /// <summary>Gets the accessible name of the button that creates a pull request.</summary>
    /// <param name="repository">The repository name.</param>
    /// <returns>The accessible name.</returns>
    public static string CreatePullRequestName(string repository) => $"Create a draft pull request for {repository}";

    /// <summary>Gets the accessible name of the button that removes a repository.</summary>
    /// <param name="repository">The repository name.</param>
    /// <returns>The accessible name.</returns>
    public static string RemoveRepositoryName(string repository) => $"Remove repository {repository}";

    /// <summary>Gets the accessible name of the button that removes a secret name.</summary>
    /// <param name="secret">The secret name.</param>
    /// <returns>The accessible name.</returns>
    public static string RemoveSecretName(string secret) => $"Remove secret name {secret}";

    /// <summary>Gets the result of a create.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string Created(string name) => $"Cloud agent {name} was created.";

    /// <summary>Gets the result of a hand-off.</summary>
    /// <param name="name">The agent name.</param>
    /// <returns>The sentence.</returns>
    public static string HandoffStarted(string name) => $"Started cloud agent {name} with your task.";

    /// <summary>Gets the reason Create is disabled because the agent cap is reached.</summary>
    /// <param name="max">The cap.</param>
    /// <returns>The sentence.</returns>
    public static string AgentCapReached(int max) =>
        string.Create(CultureInfo.InvariantCulture, $"You have reached the limit of {max} cloud {Plural(max, "agent", "agents")}. Delete one to create another.");

    /// <summary>Gets the error of a name that is too long.</summary>
    /// <param name="max">The most characters.</param>
    /// <returns>The sentence.</returns>
    public static string NameTooLong(int max) => string.Create(CultureInfo.InvariantCulture, $"Use at most {max} characters.");

    /// <summary>Gets the error of a branch that is too long.</summary>
    /// <param name="max">The most characters.</param>
    /// <returns>The sentence.</returns>
    public static string BranchTooLong(int max) => string.Create(CultureInfo.InvariantCulture, $"A branch name has at most {max} characters.");

    /// <summary>Gets the error of a secret name that is too long.</summary>
    /// <param name="max">The most characters.</param>
    /// <returns>The sentence.</returns>
    public static string SecretNameTooLong(int max) => string.Create(CultureInfo.InvariantCulture, $"A secret name has at most {max} characters.");

    /// <summary>Gets the error of too many repositories.</summary>
    /// <param name="max">The most repositories.</param>
    /// <returns>The sentence.</returns>
    public static string TooManyRepositories(int max) => string.Create(CultureInfo.InvariantCulture, $"An agent can work on at most {max} repositories.");

    /// <summary>Gets the error of too many secret names.</summary>
    /// <param name="max">The most secret names.</param>
    /// <returns>The sentence.</returns>
    public static string TooManySecretNames(int max) => string.Create(CultureInfo.InvariantCulture, $"An agent can use at most {max} secret names.");

    /// <summary>Gets the error of an idle time that is not valid.</summary>
    /// <param name="min">The smallest idle time.</param>
    /// <param name="max">The largest idle time.</param>
    /// <returns>The sentence.</returns>
    public static string IdleInvalid(int min, int max) => string.Create(CultureInfo.InvariantCulture, $"Enter a whole number of minutes from {min} to {max}.");

    /// <summary>Gets the error of a first task that is too long.</summary>
    /// <param name="max">The most characters.</param>
    /// <returns>The sentence.</returns>
    public static string PromptTooLong(int max) => string.Create(CultureInfo.InvariantCulture, $"The task can have at most {max} characters.");

    private static string Minutes(int minutes) =>
        string.Create(CultureInfo.InvariantCulture, $"{minutes} {Plural(minutes, "minute", "minutes")}");

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}
