using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>Where an agent's repositories live on its worker: <c>&lt;workspace root&gt;/&lt;session id&gt;/&lt;directory&gt;</c>. The worker creates the session directory; its bootstrap checks each repository out under it.</summary>
internal static class CloudWorkerLayout
{
    /// <summary>Gives each repository of an agent a directory name: the repository name, or <c>owner-name</c> when two repositories share a name.</summary>
    /// <param name="repositories">The agent's repositories, primary first.</param>
    /// <param name="allowedHosts">The hosts a repository URL may name.</param>
    /// <returns>The repositories as the worker is told to check them out.</returns>
    /// <exception cref="CloudProvisioningException">A URL is not acceptable.</exception>
    public static IReadOnlyList<CloudWorkerRepository> Assign(IReadOnlyList<CloudRepository> repositories, IReadOnlyCollection<string> allowedHosts)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        var parsed = new List<(CloudRepository Source, GitRepository Slug)>();
        foreach (var repository in repositories)
        {
            if (!GitRepositoryUrl.TryParse(repository.Address, allowedHosts, out var slug, out var problem) || slug is null)
            {
                throw new CloudProvisioningException("invalid_repository", problem ?? "A repository URL is not acceptable.");
            }

            parsed.Add((repository, slug));
        }

        var counts = parsed.GroupBy(static item => item.Slug.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        return parsed
            .Select(item => new CloudWorkerRepository(
                counts[item.Slug.Name] > 1 ? item.Slug.Owner + "-" + item.Slug.Name : item.Slug.Name,
                item.Slug.CloneUrl,
                item.Source.Branch,
                item.Source.BaseBranch))
            .ToList();
    }

    /// <summary>Gets the directory omp starts in: the primary repository's checkout.</summary>
    /// <param name="workspaceRoot">The worker's workspace root (<c>Cloud:Worker:WorkspaceRoot</c>).</param>
    /// <param name="agent">The agent.</param>
    /// <param name="allowedHosts">The hosts a repository URL may name.</param>
    /// <returns>The absolute path on the worker.</returns>
    public static string PrimaryWorkingDirectory(string workspaceRoot, CloudAgentRecord agent, IReadOnlyCollection<string> allowedHosts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(agent);
        var assigned = Assign(agent.Repositories, allowedHosts);
        return workspaceRoot.TrimEnd('/') + "/" + agent.SessionId + "/" + assigned[0].Directory;
    }
}
