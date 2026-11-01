namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Directories hidden from the agent by tmpfs (Linux) or a read denial (macOS): secrets, cloud and browser profiles,
/// plus the socket locations a read-only root would still let the agent connect to.
/// </summary>
public static class IsolationDeniedDirectories
{
    /// <summary>Unix socket locations covered even when they are not under the home directory.</summary>
    public static IReadOnlyList<string> SocketLocations { get; } =
    [
        "/run/user",
        "/run/docker.sock",
        "/var/run/docker.sock",
    ];

    /// <summary>Returns the denied directories under <paramref name="home"/>, in a stable order.</summary>
    /// <param name="home">The user's home directory.</param>
    /// <returns>Absolute paths. The list does not check that the directories exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="home"/> is blank.</exception>
    public static IReadOnlyList<string> ForHome(string home)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(home);
        return
        [
            IsolationPolicy.Join(home, ".ssh"),
            IsolationPolicy.Join(home, ".aws"),
            IsolationPolicy.Join(home, ".gnupg"),
            IsolationPolicy.Join(home, ".kube"),
            IsolationPolicy.Join(home, ".azure"),
            IsolationPolicy.Join(home, ".config/gcloud"),
            IsolationPolicy.Join(home, ".config/google-chrome"),
            IsolationPolicy.Join(home, ".config/chromium"),
            IsolationPolicy.Join(home, ".mozilla"),
            IsolationPolicy.Join(home, ".local/share/Autonogrammer"),
        ];
    }
}
