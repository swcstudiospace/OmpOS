using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>The ACP option adjustment that every non-ssh environment applies.</summary>
internal static class ExecSessionOptions
{
    /// <summary>
    /// Adjusts the ACP options of a session for omp in an environment: the working directory is the workspace folder in the environment, the MCP servers are the ones
    /// the launch carries, <c>FsRoot</c> is empty so omp is never served files from outside the environment, and the arguments and environment are empty because
    /// the launch carries them (the transport does not use them).
    /// </summary>
    /// <param name="launch">The launch of the session.</param>
    /// <param name="options">The options a local session would use (approval policy, handshake timeout, auth method).</param>
    /// <returns>The options for the session in the environment.</returns>
    internal static AcpClientOptions Apply(RemoteLaunch launch, AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options with
        {
            OmpPath = null,
            WorkingDirectory = launch.WorkingDirectory,
            FsRoot = null,
            ExtraArguments = [],
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [.. launch.McpServers],
        };
    }
}
