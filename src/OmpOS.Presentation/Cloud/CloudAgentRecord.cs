namespace OmpOS.Presentation.Cloud;

/// <summary>
/// A cloud agent as the host reports it. It is a snapshot: the host returns a new record when anything changes. No member holds a secret
/// value, a token or a worker address.
/// </summary>
public sealed record CloudAgentRecord
{
    /// <summary>Gets the agent identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the status.</summary>
    public required CloudAgentStatus Status { get; init; }

    /// <summary>Gets when the agent was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the repositories the agent works on, in the order the user gave them. The first is the primary one.</summary>
    public IReadOnlyList<CloudRepository> Repositories { get; init; } = [];

    /// <summary>Gets the branch the agent commits to, or null before it exists.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the branch the agent started from, or null for each repository's default branch.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>Gets the name of the environment the worker runs in, or null for the default one.</summary>
    public string? EnvironmentName { get; init; }

    /// <summary>Gets the model the agent uses, or null for the host's default.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the minutes of inactivity after which the worker sleeps. Zero when the host did not report it.</summary>
    public int IdleMinutes { get; init; }

    /// <summary>Gets when the agent last did something, or null when it has not.</summary>
    public DateTimeOffset? LastActivityAt { get; init; }

    /// <summary>Gets the identifier of the session that carries the conversation, or null until one exists. Open in Agents needs it.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the reason of the current status when the host knows one, for example the error of a failed agent.</summary>
    public string? StatusDetail { get; init; }

    /// <summary>Gets the pull requests the agent opened, at most one per repository.</summary>
    public IReadOnlyList<CloudPullRequestLink> PullRequests { get; init; } = [];

    /// <summary>Gets the status history, oldest first.</summary>
    public IReadOnlyList<CloudStatusEvent> Timeline { get; init; } = [];
}
