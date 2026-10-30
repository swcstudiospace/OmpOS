namespace Autonogrammer.Presentation.Cloud;

/// <summary>What the host knows about cloud agents before any agent exists: whether they are set up and what a new agent can choose.</summary>
public sealed record CloudSetup
{
    /// <summary>Gets a value indicating whether the cloud service is configured and signed in. False shows the setup steps instead of the agents.</summary>
    public required bool IsConfigured { get; init; }

    /// <summary>Gets the reason cloud agents are not available, in plain words, or null. Used only when <see cref="IsConfigured"/> is false.</summary>
    public string? NotConfiguredReason { get; init; }

    /// <summary>Gets the environments a new agent can run in. Empty when the host has one fixed environment.</summary>
    public IReadOnlyList<CloudChoice> Environments { get; init; } = [];

    /// <summary>Gets the identifier of the environment preselected for a new agent, or null for the first one.</summary>
    public string? DefaultEnvironmentId { get; init; }

    /// <summary>Gets the models a new agent can use. Empty when the host decides.</summary>
    public IReadOnlyList<CloudChoice> Models { get; init; } = [];

    /// <summary>Gets the names, never the values, of the secrets the user stored with the service. They are offered as suggestions.</summary>
    public IReadOnlyList<string> KnownSecretNames { get; init; } = [];

    /// <summary>Gets the minutes of inactivity after which a worker sleeps when the user does not say. The default is 15.</summary>
    public int DefaultIdleMinutes { get; init; } = 15;

    /// <summary>Gets the largest idle time the service accepts, in minutes.</summary>
    public int MaxIdleMinutes { get; init; } = 240;

    /// <summary>Creates the setup of a host that is not configured.</summary>
    /// <param name="reason">The reason in plain words, or null.</param>
    /// <returns>A setup with <see cref="IsConfigured"/> false.</returns>
    public static CloudSetup NotConfigured(string? reason = null) => new() { IsConfigured = false, NotConfiguredReason = reason };
}
