namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>Builds <see cref="RemoteLaunch"/> values for tests.</summary>
internal static class LaunchFactory
{
    /// <summary>Creates a launch with empty defaults for everything not given.</summary>
    /// <param name="workingDirectory">The remote working directory.</param>
    /// <param name="stateDirectory">The remote session state directory.</param>
    /// <param name="ompPath">The remote omp executable.</param>
    /// <param name="arguments">The arguments after <c>acp</c>.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="secretNames">The names in <paramref name="environment"/> that hold secrets.</param>
    /// <param name="files">The files written before omp starts.</param>
    /// <param name="forwards">The reverse forwards.</param>
    /// <returns>The launch.</returns>
    public static RemoteLaunch Create(
        string workingDirectory,
        string stateDirectory,
        string ompPath = "omp",
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlySet<string>? secretNames = null,
        IReadOnlyList<RemoteFile>? files = null,
        IReadOnlyList<ReverseForward>? forwards = null) =>
        new()
        {
            WorkingDirectory = workingDirectory,
            StateDirectory = stateDirectory,
            OmpPath = ompPath,
            Arguments = arguments ?? [],
            Environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
            SecretEnvironmentNames = secretNames ?? new HashSet<string>(StringComparer.Ordinal),
            Files = files ?? [],
            ReverseForwards = forwards ?? [],
            McpServers = [],
            PlanningEvents = [],
        };
}
