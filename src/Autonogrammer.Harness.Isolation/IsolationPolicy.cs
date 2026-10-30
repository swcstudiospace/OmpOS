namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// The confinement request for one session: whether isolation is on, the grants, the scratch directory and the omp state directories.
/// Cowork defaults to on. Code defaults to off. A read-only grant is not a write bind.
/// </summary>
public sealed record IsolationPolicy
{
    /// <summary>Gets a value indicating whether this session wants confinement. False for Code, true for Cowork.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the operating system the wrappers and the planner target.</summary>
    public IsolationPlatform Platform { get; init; } = IsolationPlatform.Linux;

    /// <summary>Gets the user's home directory. Denied directories and <c>~/.omp</c> are resolved under it.</summary>
    public string HomeDirectory { get; init; } = string.Empty;

    /// <summary>Gets the session scratch directory, bound read-write when isolation is on.</summary>
    public string? ScratchDirectory { get; init; }

    /// <summary>Gets the directory named by <c>PI_CODING_AGENT_DIR</c> when that variable is set, bound read-write.</summary>
    public string? CodingAgentDirectory { get; init; }

    /// <summary>Gets the folder grants. Only entries with <see cref="IsolationGrant.ReadWrite"/> become write binds.</summary>
    public IReadOnlyList<IsolationGrant> Grants { get; init; } = [];

    /// <summary>Gets a Code policy: isolation off, no wrapper, no probe required.</summary>
    /// <returns>The Code default.</returns>
    public static IsolationPolicy ForCode() => new() { Enabled = false, Platform = IsolationPlatform.Linux };

    /// <summary>Gets a Cowork policy: isolation on, with the home, scratch and grants the session will use.</summary>
    /// <param name="homeDirectory">The user's home directory.</param>
    /// <param name="scratchDirectory">The session scratch directory.</param>
    /// <param name="grants">The folder grants. Null is treated as none.</param>
    /// <param name="codingAgentDirectory">The <c>PI_CODING_AGENT_DIR</c> directory, or null when unset.</param>
    /// <param name="platform">The operating system. Defaults to Linux.</param>
    /// <returns>The Cowork default.</returns>
    /// <exception cref="ArgumentException">A required path is blank.</exception>
    public static IsolationPolicy ForCowork(
        string homeDirectory,
        string scratchDirectory,
        IReadOnlyList<IsolationGrant>? grants = null,
        string? codingAgentDirectory = null,
        IsolationPlatform platform = IsolationPlatform.Linux)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchDirectory);
        return new IsolationPolicy
        {
            Enabled = true,
            Platform = platform,
            HomeDirectory = homeDirectory,
            ScratchDirectory = scratchDirectory,
            CodingAgentDirectory = string.IsNullOrWhiteSpace(codingAgentDirectory) ? null : codingAgentDirectory,
            Grants = grants ?? [],
        };
    }

    /// <summary>Gets the directories bound read-write because omp needs them: <c>~/.omp</c> and, when set, <c>PI_CODING_AGENT_DIR</c>.</summary>
    /// <returns>The state directories, in that order.</returns>
    public IReadOnlyList<string> StateDirectories()
    {
        var directories = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(HomeDirectory))
        {
            directories.Add(Join(HomeDirectory, ".omp"));
        }

        if (!string.IsNullOrWhiteSpace(CodingAgentDirectory))
        {
            directories.Add(CodingAgentDirectory);
        }

        return directories;
    }

    /// <summary>Joins a home directory and a relative path with <c>/</c> so mount arguments do not depend on the host separator.</summary>
    /// <param name="home">The home directory.</param>
    /// <param name="relative">The path under home, already using <c>/</c>.</param>
    /// <returns>The absolute path.</returns>
    internal static string Join(string home, string relative)
    {
        var root = home.TrimEnd('/');
        return root + "/" + relative.TrimStart('/');
    }
}
