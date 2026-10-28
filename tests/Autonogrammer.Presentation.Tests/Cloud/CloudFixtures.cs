using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Tests.AgentPanel;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>Builders shared by the cloud tests: agents, repositories and loaded models over a <see cref="FakeCloudAgentsHost"/>.</summary>
internal static class CloudFixtures
{
    /// <summary>The first reading of a <see cref="ManualTimeProvider"/> created without a start.</summary>
    internal static readonly DateTimeOffset Origin = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a repository on GitHub.</summary>
    /// <param name="name">The <c>owner/repository</c> name.</param>
    /// <returns>The repository.</returns>
    internal static CloudRepository Repo(string name = "owner/app") => new($"https://github.com/{name}");

    /// <summary>Creates an agent record created and last active at <see cref="Origin"/>.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="status">The status.</param>
    /// <param name="name">The name, or null for <c>Agent {id}</c>.</param>
    /// <param name="sessionId">The session, or null for <c>session-{id}</c>.</param>
    /// <returns>The record.</returns>
    internal static CloudAgentRecord Agent(string id, CloudAgentStatus status, string? name = null, string? sessionId = null) => new()
    {
        Id = id,
        Name = name ?? $"Agent {id}",
        Status = status,
        CreatedAt = Origin,
        LastActivityAt = Origin,
        Repositories = [Repo()],
        Branch = $"autonogrammer/{id}",
        IdleMinutes = 15,
        SessionId = sessionId ?? $"session-{id}",
        Timeline = [new CloudStatusEvent(Origin, CloudAgentStatus.Provisioning), new CloudStatusEvent(Origin, status)],
    };

    /// <summary>Creates a host holding the given agents.</summary>
    /// <param name="clock">The clock of the host.</param>
    /// <param name="agents">The agents.</param>
    /// <returns>The host.</returns>
    internal static FakeCloudAgentsHost Host(ManualTimeProvider clock, params CloudAgentRecord[] agents)
    {
        var host = new FakeCloudAgentsHost(clock);
        foreach (var agent in agents)
        {
            host.Add(agent);
        }

        return host;
    }

    /// <summary>Creates a view model over the host and loads it.</summary>
    /// <param name="host">The host.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="cancellationToken">The test token.</param>
    /// <returns>The loaded model.</returns>
    internal static async Task<CloudAgentsViewModel> LoadedAsync(FakeCloudAgentsHost host, ManualTimeProvider clock, CancellationToken cancellationToken)
    {
        var model = new CloudAgentsViewModel(host, clock);
        await model.LoadAsync(cancellationToken).ConfigureAwait(true);
        return model;
    }

    /// <summary>Creates a new-agent model whose entries are valid: a name and one repository. The cost is not confirmed.</summary>
    /// <param name="host">The host.</param>
    /// <param name="setup">The setup, or null for a configured host with nothing to choose.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The model.</returns>
    internal static NewCloudAgentModel Form(FakeCloudAgentsHost host, CloudSetup? setup = null, CloudLimits? limits = null)
    {
        var model = new NewCloudAgentModel(host, setup ?? new CloudSetup { IsConfigured = true }, limits)
        {
            Name = "Fix the build",
            RepositoryText = "https://github.com/owner/app",
        };
        _ = model.AddRepository();
        return model;
    }
}
