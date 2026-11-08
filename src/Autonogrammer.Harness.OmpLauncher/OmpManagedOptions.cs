namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Everything the managed omp install needs from its host: the directory it owns, the registry and GitHub endpoints, and the
/// seams for HTTP, processes, files and time. The head supplies <paramref name="RootDirectory"/>; nothing here is a per-user path.
/// </summary>
/// <param name="RootDirectory">The absolute directory the managed install owns. Every write lands below its <c>omp</c> child.</param>
/// <param name="RegistryBaseUri">The npm registry base with a trailing slash, such as <c>https://registry.npmjs.org/</c>.</param>
/// <param name="GitHubBaseUri">The GitHub base with a trailing slash, such as <c>https://github.com/</c>.</param>
/// <param name="HttpClient">The client for the packument, the tarball and the Bun archive. Its own timeout also applies.</param>
/// <param name="ProcessRunner">Runs <c>bun install</c>, <c>bun build --compile</c> and the omp health probes.</param>
/// <param name="FileSystem">Moves staged directories into place and reports locked files.</param>
/// <param name="Clock">The time source for the state record and the last-checked notice.</param>
public sealed record OmpManagedOptions(
    string RootDirectory,
    Uri RegistryBaseUri,
    Uri GitHubBaseUri,
    HttpClient HttpClient,
    IOmpProcessRunner ProcessRunner,
    IOmpFileSystem FileSystem,
    TimeProvider Clock)
{
    /// <summary>The pinned Bun. Defaults to <see cref="OmpBunPin.Default"/>.</summary>
    public OmpBunPin BunPin { get; init; } = OmpBunPin.Default;

    /// <summary>The platform key used to pick the Bun archive. Null means no Bun is pinned here.</summary>
    public string? PlatformKey { get; init; } = OmpBunPin.CurrentPlatformKey();

    /// <summary>The time allowed for a registry read. A longer wait counts as offline.</summary>
    public TimeSpan NetworkTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The time allowed for one tarball or Bun archive download.</summary>
    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>The time allowed for <c>bun install</c> and <c>bun build --compile</c>.</summary>
    public TimeSpan ProcessTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>The time allowed for <c>omp --version</c>.</summary>
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Checks every value. The installer and the resolver call it before any work.</summary>
    /// <exception cref="ArgumentNullException">A required seam is null.</exception>
    /// <exception cref="ArgumentException">A path or base address is malformed, or the pinned Bun version is not a version.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A timeout is not positive.</exception>
    internal void Validate()
    {
        RequireNotNull(HttpClient, nameof(HttpClient));
        RequireNotNull(ProcessRunner, nameof(ProcessRunner));
        RequireNotNull(FileSystem, nameof(FileSystem));
        RequireNotNull(Clock, nameof(Clock));
        RequireNotNull(BunPin, nameof(BunPin));
        RequireRootDirectory(RootDirectory, nameof(RootDirectory));
        RequireBase(RegistryBaseUri, nameof(RegistryBaseUri));
        RequireBase(GitHubBaseUri, nameof(GitHubBaseUri));
        RequireBunVersion(BunPin.Version, nameof(BunPin));
        RequirePositive(NetworkTimeout, nameof(NetworkTimeout));
        RequirePositive(DownloadTimeout, nameof(DownloadTimeout));
        RequirePositive(ProcessTimeout, nameof(ProcessTimeout));
        RequirePositive(HealthCheckTimeout, nameof(HealthCheckTimeout));
    }

    private static void RequireNotNull(object? value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
    }

    private static void RequireRootDirectory(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
        {
            throw new ArgumentException("The managed root directory must be an absolute path.", name);
        }
    }

    private static void RequireBunVersion(string version, string name)
    {
        if (!OmpSemver.TryParse(version, out _))
        {
            throw new ArgumentException("The pinned Bun version must be a plain semantic version.", name);
        }
    }

    private static void RequireBase(Uri uri, string name)
    {
        if (!uri.IsAbsoluteUri || !uri.AbsolutePath.EndsWith('/'))
        {
            throw new ArgumentException("The base address must be absolute and end with a slash.", name);
        }
    }

    private static void RequirePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, "The timeout must be positive.");
        }
    }
}
