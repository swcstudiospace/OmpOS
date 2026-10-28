namespace OmpOS.Presentation.Cloud;

/// <summary>What the user asks for when a cloud agent is created. It carries secret names only, never their values.</summary>
public sealed record CloudAgentRequest
{
    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the repositories, at least one. The first is the primary repository.</summary>
    public required IReadOnlyList<CloudRepository> Repositories { get; init; }

    /// <summary>Gets the branch the agent commits to, or null to let the host name one.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the branch the agent starts from, or null for each repository's default branch.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>Gets the environment identifier, or null for the host's default.</summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the names of the stored secrets the worker may read. The values never pass through the app.</summary>
    public IReadOnlyList<string> SecretNames { get; init; } = [];

    /// <summary>Gets the minutes of inactivity after which the worker sleeps.</summary>
    public int IdleMinutes { get; init; } = 15;

    /// <summary>Gets the model identifier, or null for the host's default.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the first task for the agent, or null to start without one.</summary>
    public string? Prompt { get; init; }
}
