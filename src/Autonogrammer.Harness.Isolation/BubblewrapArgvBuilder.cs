namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Builds the bubblewrap argument vector: a read-only root, one bind per read-write grant, tmpfs over denied directories
/// and socket locations, and the unshare flags. It does not start a process. The environment scrub lives here so both
/// OS wrappers apply the same names after the engine environment policy.
/// </summary>
public static class BubblewrapArgvBuilder
{
    /// <summary>Environment names removed from the child even when an allow-list would have kept them.</summary>
    public static IReadOnlyList<string> ScrubbedNames { get; } =
    [
        "SSH_AUTH_SOCK",
        "DBUS_SESSION_BUS_ADDRESS",
        "DISPLAY",
        "WAYLAND_DISPLAY",
    ];

    /// <summary>Builds the confinement arguments for <paramref name="policy"/>, without the intended command.</summary>
    /// <param name="policy">The session policy. Read-only grants are not bound.</param>
    /// <returns>The argument vector, one element each, in the golden order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public static IReadOnlyList<string> Build(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var argv = new List<string>
        {
            "--ro-bind", "/", "/",
        };

        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite && !string.IsNullOrWhiteSpace(grant.Path))
            {
                argv.Add("--bind");
                argv.Add(grant.Path);
            }
        }

        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            argv.Add("--bind");
            argv.Add(policy.ScratchDirectory);
        }

        foreach (var state in policy.StateDirectories())
        {
            argv.Add("--bind");
            argv.Add(state);
        }

        if (!string.IsNullOrWhiteSpace(policy.HomeDirectory))
        {
            foreach (var denied in IsolationDeniedDirectories.ForHome(policy.HomeDirectory))
            {
                argv.Add("--tmpfs");
                argv.Add(denied);
            }
        }

        foreach (var socket in IsolationDeniedDirectories.SocketLocations)
        {
            argv.Add("--tmpfs");
            argv.Add(socket);
        }

        argv.Add("--dev");
        argv.Add("/dev");
        argv.Add("--proc");
        argv.Add("/proc");
        argv.Add("--tmpfs");
        argv.Add("/tmp");
        argv.Add("--unshare-pid");
        argv.Add("--unshare-ipc");
        argv.Add("--unshare-uts");
        argv.Add("--die-with-parent");
        argv.Add("--new-session");
        return argv;
    }

    /// <summary>Copies <paramref name="environment"/> and sets the scrubbed names to null so the transport unsets them.</summary>
    /// <param name="environment">The environment the engine policy already applied.</param>
    /// <returns>A new dictionary. The input is not modified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public static IReadOnlyDictionary<string, string?> Scrub(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var scrubbed = new Dictionary<string, string?>(environment, StringComparer.Ordinal);
        foreach (var name in ScrubbedNames)
        {
            scrubbed[name] = null;
        }

        return scrubbed;
    }
}
