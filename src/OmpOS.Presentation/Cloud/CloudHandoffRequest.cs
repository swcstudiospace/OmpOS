namespace OmpOS.Presentation.Cloud;

/// <summary>A prompt to continue in the cloud, for example from the <c>&amp;</c> prefix or the in-cloud command.</summary>
public sealed record CloudHandoffRequest
{
    /// <summary>Gets the prompt, which becomes the first task of the new agent.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets the repositories, at least one. The caller picks them, usually from the session's folder.</summary>
    public required IReadOnlyList<CloudRepository> Repositories { get; init; }

    /// <summary>Gets whether the user confirmed the cost shown by <see cref="CloudCopy.CostNotice"/>. A hand-off without it makes no request.</summary>
    public required bool CostConfirmed { get; init; }

    /// <summary>Gets the agent name, or null to derive it from the first line of the prompt.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the branch the agent commits to, or null.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the branch the agent starts from, or null.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>Gets the environment identifier, or null for the host's default.</summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Gets the model identifier, or null for the host's default.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the names of the stored secrets the worker may read.</summary>
    public IReadOnlyList<string> SecretNames { get; init; } = [];

    /// <summary>Gets the idle minutes, or null for the service default.</summary>
    public int? IdleMinutes { get; init; }
}
