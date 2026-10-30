using System.Text.Json;
using Autonogrammer.Api.Cloud.Options;
using Autonogrammer.Contracts.Cloud;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Builds the variables of a worker: the session pin, the agent's secrets, the git credential (an installation token limited to the agent's
/// repositories, or the configured token), the repository list, the protected default branches and the resolved environment configuration. Nothing
/// here is executed. The result holds secrets; it goes to the provider and is not stored or logged.
/// </summary>
internal sealed class CloudWorkerVariables : ICloudWorkerVariableSource
{
    private readonly ICloudAgentStore _store;
    private readonly CloudSecretResolver _secrets;
    private readonly IGitCredentialSource _credentials;
    private readonly IGitHostClient _git;
    private readonly IOptionsMonitor<CloudOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="CloudWorkerVariables"/> class.</summary>
    /// <param name="store">The agent store.</param>
    /// <param name="secrets">The secret resolver.</param>
    /// <param name="credentials">The git credential source.</param>
    /// <param name="git">The git host client.</param>
    /// <param name="options">The cloud options.</param>
    public CloudWorkerVariables(ICloudAgentStore store, CloudSecretResolver secrets, IGitCredentialSource credentials, IGitHostClient git, IOptionsMonitor<CloudOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _secrets = secrets;
        _credentials = credentials;
        _git = git;
        _options = options;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string>> ResolveAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerVariableNames.SessionId] = sessionId };
        var agent = await _store.GetBySessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (agent is null)
        {
            return variables;
        }

        var settings = _options.CurrentValue;
        var repositories = CloudWorkerLayout.Assign(agent.Repositories, settings.Git.AllowedHosts);
        var slugs = ParseSlugs(agent, settings);

        var resolution = await _secrets.ResolveAsync(agent.OwnerId, agent.SecretRefs, cancellationToken).ConfigureAwait(false);
        if (resolution.Missing.Count > 0)
        {
            throw new CloudProvisioningException("secret_missing", "A secret the agent references no longer exists: " + string.Join(", ", resolution.Missing) + ".");
        }

        foreach (var (name, value) in resolution.Values)
        {
            variables[name] = value;
        }

        variables[CloudWorkerVariableNames.AgentId] = agent.Id;
        variables[CloudWorkerVariableNames.AgentBranch] = agent.Branch;
        variables[CloudWorkerVariableNames.Repositories] = JsonSerializer.Serialize(repositories.ToArray(), CloudWorkerJsonContext.Default.CloudWorkerRepositoryArray);
        variables[CloudWorkerVariableNames.SecretNames] = JsonSerializer.Serialize(agent.SecretRefs.ToArray(), CloudWorkerJsonContext.Default.StringArray);

        var protectedBranches = await AddGitAsync(variables, slugs, cancellationToken).ConfigureAwait(false);
        variables[CloudWorkerVariableNames.ProtectedBranches] = JsonSerializer.Serialize(protectedBranches, CloudWorkerJsonContext.Default.StringArray);

        var environment = await ResolveEnvironmentAsync(agent, slugs, settings, cancellationToken).ConfigureAwait(false);
        if (environment.Errors.Count > 0)
        {
            throw new CloudProvisioningException("invalid_environment_config", string.Join(" ", environment.Errors));
        }

        if (environment.Config is not null)
        {
            variables[CloudWorkerVariableNames.Environment] = JsonSerializer.Serialize(environment.Config, CloudWorkerJsonContext.Default.EnvironmentConfig);
        }

        return variables;
    }

    private async Task<string[]> AddGitAsync(Dictionary<string, string> variables, List<GitRepository> slugs, CancellationToken cancellationToken)
    {
        var protectedBranches = new List<string>();
        if (!_credentials.IsConfigured)
        {
            return [];
        }

        try
        {
            var credential = await _credentials.GetAsync(slugs, GitPermissions.Push, cancellationToken).ConfigureAwait(false);
            variables[CloudWorkerVariableNames.GitToken] = credential.Token;
            variables[CloudWorkerVariableNames.GitTerminalPrompt] = "0";
            foreach (var slug in slugs.DistinctBy(static slug => slug.CloneUrl, StringComparer.OrdinalIgnoreCase))
            {
                var defaultBranch = await _git.GetDefaultBranchAsync(slug, cancellationToken).ConfigureAwait(false);
                if (!protectedBranches.Contains(defaultBranch, StringComparer.Ordinal))
                {
                    protectedBranches.Add(defaultBranch);
                }
            }
        }
        catch (GitHostException ex)
        {
            throw new CloudProvisioningException("git_host_error", ex.Message);
        }

        return [.. protectedBranches];
    }

    private static List<GitRepository> ParseSlugs(CloudAgentRecord agent, CloudOptions settings)
    {
        var slugs = new List<GitRepository>();
        foreach (var repository in agent.Repositories)
        {
            if (!GitRepositoryUrl.TryParse(repository.Address, settings.Git.AllowedHosts, out var slug, out var problem) || slug is null)
            {
                throw new CloudProvisioningException("invalid_repository", problem ?? "A repository URL is not acceptable.");
            }

            slugs.Add(slug);
        }

        return slugs;
    }

    private async Task<ResolvedEnvironment> ResolveEnvironmentAsync(CloudAgentRecord agent, List<GitRepository> slugs, CloudOptions settings, CancellationToken cancellationToken)
    {
        EnvironmentConfig? personal = null;
        if (agent.EnvironmentId is { } environmentId)
        {
            if (!settings.Environments.TryGetValue(environmentId, out var definition))
            {
                throw new CloudProvisioningException("unknown_environment", "The saved environment the agent was created with no longer exists.");
            }

            personal = new EnvironmentConfig(
                null,
                definition.Install,
                definition.Start,
                [.. definition.Terminals.Select(static terminal => new EnvironmentTerminal(terminal.Name, terminal.Command))],
                null,
                "personal");
        }

        string? autonogrammerFile = null;
        string? cursorFile = null;
        if (personal is null && slugs.Count == 1)
        {
            try
            {
                var reference = agent.Repositories[0].Branch;
                autonogrammerFile = await _git.ReadFileAsync(slugs[0], EnvironmentConfigFile.AutonogrammerPath, reference, cancellationToken).ConfigureAwait(false);
                cursorFile = await _git.ReadFileAsync(slugs[0], EnvironmentConfigFile.CursorPath, reference, cancellationToken).ConfigureAwait(false);
            }
            catch (GitHostException ex) when (ex.Failure == GitHostFailure.NotConfigured)
            {
                // Without a git credential the repository files cannot be read; the worker may still find them after checkout.
                autonogrammerFile = null;
                cursorFile = null;
            }
            catch (GitHostException ex)
            {
                throw new CloudProvisioningException("git_host_error", ex.Message);
            }
        }

        return EnvironmentConfigResolver.Resolve(personal, slugs.Count, autonogrammerFile, cursorFile);
    }
}
