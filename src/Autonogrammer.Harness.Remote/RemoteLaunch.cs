using System.Globalization;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// How omp is started on the remote machine for one session: the result of <see cref="RemoteLaunchMaterializer"/> and the input of
/// <see cref="SshCommandBuilder"/>. The environment may hold provider tokens, so the type never prints values.
/// </summary>
public sealed record RemoteLaunch
{
    /// <summary>Gets the absolute POSIX working directory on the remote machine.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the omp executable on the remote machine: an absolute path or a bare command name.</summary>
    public required string OmpPath { get; init; }

    /// <summary>Gets the arguments that follow <c>acp</c>, one word each. They never contain a secret.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the per-session state directory on the remote machine (mode 0700), created before omp starts.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the environment variables set for omp. They travel in the stdin preamble and never in an argument. Values may be secrets.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the names in <see cref="Environment"/> whose values are secrets (provider tokens).</summary>
    public required IReadOnlySet<string> SecretEnvironmentNames { get; init; }

    /// <summary>Gets the files written on the remote machine before omp starts.</summary>
    public required IReadOnlyList<RemoteFile> Files { get; init; }

    /// <summary>Gets the reverse port forwards the connection carries.</summary>
    public required IReadOnlyList<ReverseForward> ReverseForwards { get; init; }

    /// <summary>Gets the MCP servers for <c>session/new</c> and <c>session/load</c>, with local loopback addresses rewritten to the remote loopback ports.</summary>
    public required IReadOnlyList<AcpHttpMcpServer> McpServers { get; init; }

    /// <summary>Gets the degradations decided while planning, plus what the remote launch left out (for example plugins that cannot run there).</summary>
    public required IReadOnlyList<StageEvent> PlanningEvents { get; init; }

    /// <summary>Gets the identifiers of the plugins that load on the remote machine.</summary>
    public IReadOnlyList<string> LoadedPluginIds { get; init; } = [];

    /// <summary>Gets the secret values, so a diagnostic text can have them removed. The list is empty when no secret was given.</summary>
    /// <returns>The secret values.</returns>
    public IReadOnlyList<string> GetRedactionValues()
    {
        var values = new List<string>(SecretEnvironmentNames.Count);
        foreach (var name in SecretEnvironmentNames)
        {
            if (Environment.TryGetValue(name, out var value) && value.Length > 0)
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <summary>Describes the launch without any value: counts and environment variable names.</summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"RemoteLaunch {{ Arguments = {Arguments.Count}, Environment = [{string.Join(", ", Environment.Keys.Order(StringComparer.Ordinal))}], Files = {Files.Count}, ReverseForwards = {ReverseForwards.Count}, McpServers = {McpServers.Count} }}");
}
