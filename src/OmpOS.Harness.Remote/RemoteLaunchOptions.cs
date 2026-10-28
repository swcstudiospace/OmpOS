namespace OmpOS.Harness.Remote;

/// <summary>What <see cref="RemoteLaunchMaterializer"/> needs to know beyond the launch plan.</summary>
public sealed record RemoteLaunchOptions
{
    /// <summary>Gets the identifier of the session. It names the per-session directory on the remote machine and must be a safe path segment.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the home directory of the remote user, from <see cref="OmpOS.Contracts.Environments.RemotePreflightReport.HomeDirectory"/>.</summary>
    public required string RemoteHomeDirectory { get; init; }

    /// <summary>
    /// Gets the state root the launch plan was made with: the directory that holds <c>sessions/&lt;id&gt;</c> on this machine. Values in the plan
    /// that point into the session directory are moved to the remote session directory.
    /// </summary>
    public required string LocalStateRoot { get; init; }

    /// <summary>Gets where local plugin directories are installed on the remote machine. Plugin entries outside every mapping are left out of the remote launch.</summary>
    public IReadOnlyList<RemotePluginMapping> PluginMappings { get; init; } = [];

    /// <summary>Gets the first remote loopback port used for reverse forwards. Defaults to 41000.</summary>
    public int ReversePortBase { get; init; } = 41000;

    /// <summary>Gets how many ports from <see cref="ReversePortBase"/> are used. A session's ports are chosen inside this range from its identifier. Defaults to 2000.</summary>
    public int ReversePortSpan { get; init; } = 2000;
}
