using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The inputs shared by every type that reads or changes omp's own configuration: where omp is, which agent directory it
/// uses, and the limits applied to each omp invocation. The defaults match a normal installation.
/// </summary>
public sealed class OmpConfigOptions
{
    /// <summary>The default time one omp invocation may run before its process tree is killed.</summary>
    public static TimeSpan DefaultCommandTimeout { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The default limit, in bytes, for the captured standard output and for the captured standard error.</summary>
    public const int DefaultMaxOutputBytes = 4 * 1024 * 1024;

    /// <summary>Gets how omp is located. Null uses <see cref="OmpLocatorOptions.FromEnvironment"/> with no configured path.</summary>
    public OmpLocatorOptions? Locator { get; init; }

    /// <summary>
    /// Gets the omp agent directory that holds <c>models.yml</c>, <c>config.yml</c> and the credential database. Null resolves, in
    /// order: the directory below <see cref="Home"/> when <see cref="Home"/> is set, the <c>PI_CODING_AGENT_DIR</c> environment
    /// variable of this process, then <c>~/.omp/agent</c>. Every omp invocation receives the resolved value as
    /// <c>PI_CODING_AGENT_DIR</c>, so the files this library edits and the omp that reads them always agree.
    /// </summary>
    public string? AgentDirectory { get; init; }

    /// <summary>Gets the home directory given to omp as <c>HOME</c>. Null leaves the host's value. Intended for tests and sandboxes.</summary>
    public string? Home { get; init; }

    /// <summary>Gets the time one omp invocation may run. The default is 30 seconds.</summary>
    public TimeSpan CommandTimeout { get; init; } = DefaultCommandTimeout;

    /// <summary>Gets the most bytes kept from the standard output of one invocation, and from its standard error. The default is 4 MiB.</summary>
    public int MaxOutputBytes { get; init; } = DefaultMaxOutputBytes;

    /// <summary>Gets the clock used for command timeouts and backup names. Null uses <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Resolves the clock.</summary>
    /// <returns>The configured clock, or the system clock.</returns>
    internal TimeProvider Clock() => TimeProvider ?? System.TimeProvider.System;

    /// <summary>Resolves the home directory that omp would see: <see cref="Home"/>, else the user's profile directory.</summary>
    /// <returns>The home directory, possibly empty when the host has none.</returns>
    internal string ResolveHome() =>
        !string.IsNullOrWhiteSpace(Home) ? Home : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Resolves the agent directory as documented on <see cref="AgentDirectory"/>.</summary>
    /// <returns>The absolute agent directory.</returns>
    internal string ResolveAgentDirectory()
    {
        if (!string.IsNullOrWhiteSpace(AgentDirectory))
        {
            return Path.GetFullPath(AgentDirectory);
        }

        if (!string.IsNullOrWhiteSpace(Home))
        {
            return Path.GetFullPath(Path.Combine(Home, ".omp", "agent"));
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("PI_CODING_AGENT_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        return Path.GetFullPath(Path.Combine(ResolveHome(), ".omp", "agent"));
    }
}
