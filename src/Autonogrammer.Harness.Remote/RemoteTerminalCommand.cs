namespace Autonogrammer.Harness.Remote;

/// <summary>An interactive ssh terminal ready to start, and what it could not carry from the launch.</summary>
public sealed record RemoteTerminalCommand
{
    /// <summary>Gets the ssh process to run in a pseudo-terminal. It never holds a secret.</summary>
    public required SshInvocation Invocation { get; init; }

    /// <summary>
    /// Gets the names of the environment variables of the launch that were left out: secrets (a terminal has no private channel for them: the remote omp uses
    /// its own login), and names or values a remote shell line cannot carry.
    /// </summary>
    public required IReadOnlyList<string> OmittedEnvironmentNames { get; init; }

    /// <summary>Gets whether a settings overlay file of the launch was left out because it can only travel on the private channel of an ACP session.</summary>
    public bool ConfigOverlayOmitted { get; init; }
}
