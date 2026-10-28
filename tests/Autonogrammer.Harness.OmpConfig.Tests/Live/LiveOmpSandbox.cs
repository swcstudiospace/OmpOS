using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.OmpConfig.Tests.Live;

/// <summary>
/// A temporary home for one live run: the agent directory omp reads and writes lives below it, so nothing under the real home is
/// read or written. The real omp found on PATH (or in a known install location) is used. The directory is deleted when disposed.
/// </summary>
internal sealed class LiveOmpSandbox : IDisposable
{
    /// <summary>The key every live test imports. It is fake and never accepted by any provider.</summary>
    public const string FakeKey = "sk-fake-0000-test";

    public LiveOmpSandbox()
    {
        Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "autonogrammer-live-ompconfig-" + Guid.NewGuid().ToString("N")));
        Home = Path.Combine(Root, "home");
        AgentDirectory = Path.Combine(Home, ".omp", "agent");
        _ = Directory.CreateDirectory(AgentDirectory);
    }

    /// <summary>Gets the sandbox root.</summary>
    public string Root { get; }

    /// <summary>Gets the directory that replaces the user's home.</summary>
    public string Home { get; }

    /// <summary>Gets the agent directory omp is pointed at.</summary>
    public string AgentDirectory { get; }

    /// <summary>Gets the path of <c>models.yml</c> in the sandbox.</summary>
    public string ModelsYmlPath => Path.Combine(AgentDirectory, "models.yml");

    /// <summary>Finds the real omp.</summary>
    /// <returns>The location of omp, or null when it is not installed.</returns>
    public static string? FindOmp() =>
        OmpLocator.Locate(OmpLocatorOptions.FromEnvironment(null)) is OmpFound found ? found.ExecutablePath : null;

    /// <summary>Builds options that run the real omp inside the sandbox with a generous time limit for a cold start.</summary>
    /// <returns>The options.</returns>
    public OmpConfigOptions Options() =>
        new()
        {
            Locator = OmpLocatorOptions.FromEnvironment(null),
            AgentDirectory = AgentDirectory,
            Home = Home,
            CommandTimeout = TimeSpan.FromSeconds(180),
        };

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
