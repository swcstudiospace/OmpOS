namespace Autonogrammer.Api.Cloud;

/// <summary>The names of the environment variables the control plane gives a worker besides the agent's own secrets. The worker image's bootstrap reads them; none is a secret except <see cref="GitToken"/>.</summary>
internal static class CloudWorkerVariableNames
{
    /// <summary>The worker's own setting that pins it to one session: <c>Worker:SessionId</c>.</summary>
    public const string SessionId = "Worker__SessionId";

    /// <summary>The agent identifier.</summary>
    public const string AgentId = "AUTONOGRAMMER_AGENT_ID";

    /// <summary>The branch to create and push in every repository.</summary>
    public const string AgentBranch = "AUTONOGRAMMER_AGENT_BRANCH";

    /// <summary>The JSON list of repositories to check out: <c>[{directory, url, branch, baseBranch}]</c>.</summary>
    public const string Repositories = "AUTONOGRAMMER_REPOSITORIES";

    /// <summary>The JSON list of default branch names a push must be refused for.</summary>
    public const string ProtectedBranches = "AUTONOGRAMMER_PROTECTED_BRANCHES";

    /// <summary>The JSON of the resolved environment configuration (install, start, terminals); absent when none applies.</summary>
    public const string Environment = "AUTONOGRAMMER_ENVIRONMENT";

    /// <summary>The JSON list of the names of the secrets in the environment, for the worker's own redaction.</summary>
    public const string SecretNames = "AUTONOGRAMMER_SECRET_NAMES";

    /// <summary>The git credential for clone and push, an installation token or a personal access token. A secret.</summary>
    public const string GitToken = "AUTONOGRAMMER_GIT_TOKEN";

    /// <summary>Set to <c>0</c> so git never prompts for a credential on the worker.</summary>
    public const string GitTerminalPrompt = "GIT_TERMINAL_PROMPT";
}
