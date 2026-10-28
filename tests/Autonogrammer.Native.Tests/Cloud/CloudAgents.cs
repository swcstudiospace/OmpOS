using Autonogrammer.Presentation.Cloud;

namespace Autonogrammer.Native.Tests.Cloud;

/// <summary>Builders of the agents the cloud view tests draw.</summary>
internal static class CloudAgents
{
    /// <summary>The first reading of the tests' clock.</summary>
    internal static readonly DateTimeOffset Origin = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Creates an agent record created and last active at <see cref="Origin"/>.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="status">The status.</param>
    /// <param name="name">The name.</param>
    /// <returns>The record.</returns>
    internal static CloudAgentRecord Agent(string id, CloudAgentStatus status, string name) => new()
    {
        Id = id,
        Name = name,
        Status = status,
        CreatedAt = Origin,
        LastActivityAt = Origin,
        Repositories = [new CloudRepository("https://github.com/owner/app")],
        Branch = $"autonogrammer/{id}",
        BaseBranch = "main",
        EnvironmentName = "Production",
        IdleMinutes = 15,
        SessionId = $"session-{id}",
        Timeline = [new CloudStatusEvent(Origin, CloudAgentStatus.Provisioning), new CloudStatusEvent(Origin, status)],
    };
}
