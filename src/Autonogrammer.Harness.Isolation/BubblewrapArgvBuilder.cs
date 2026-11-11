namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Builds the bubblewrap argument vector. Mount order carries the precedence: the read-only root first, then
/// <c>/tmp</c> as tmpfs so later grants under the temporary directory are not shadowed, then read-write binds
/// (grants, scratch, state), then read-only grant re-binds so a nested read-only grant wins over its read-write
/// parent, and finally the deny masks. A deny target that is a file (the docker socket paths) is masked with a
/// read-only <c>/dev/null</c> bind because tmpfs needs a directory. It does not start a process. The environment
/// scrub lives here so both OS wrappers apply the same names after the engine environment policy.
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

    /// <summary>The null device bound read-only over socket file targets the child must not reach.</summary>
    public const string SocketMaskSource = "/dev/null";

    /// <summary>Builds the canonical confinement arguments for <paramref name="policy"/>, without the intended command.</summary>
    /// <param name="policy">The session policy. Read-only grants are re-bound read-only after read-write binds.</param>
    /// <returns>The argument vector, one element each, in the golden order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public static IReadOnlyList<string> Build(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var argv = new List<string>
        {
            "--ro-bind", "/", "/",
            "--tmpfs", "/tmp",
        };

        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite && !string.IsNullOrWhiteSpace(grant.Path))
            {
                Bind(argv, "--bind", grant.Path);
            }
        }

        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            Bind(argv, "--bind", policy.ScratchDirectory);
        }

        foreach (var state in policy.StateDirectories())
        {
            Bind(argv, "--bind", state);
        }

        foreach (var grant in policy.Grants)
        {
            if (!grant.ReadWrite && !string.IsNullOrWhiteSpace(grant.Path))
            {
                Bind(argv, "--ro-bind", grant.Path);
            }
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
        argv.Add("--unshare-pid");
        argv.Add("--unshare-ipc");
        argv.Add("--unshare-uts");
        argv.Add("--die-with-parent");
        argv.Add("--new-session");
        return argv;
    }

    /// <summary>
    /// Adapts the canonical argv to one host at launch time. Deny targets that do not exist are skipped (nothing to
    /// hide), a deny target that is a file becomes a <see cref="SocketMaskSource"/> bind (tmpfs needs a directory),
    /// and a read-only grant that does not exist is skipped because the read-only root already shows nothing. A
    /// read-write bind source that does not exist fails closed: the session cannot satisfy its own policy.
    /// </summary>
    /// <param name="policy">The same session policy <see cref="Build"/> received.</param>
    /// <param name="argv">The canonical argv to adapt.</param>
    /// <returns>A new argv list. The input is not modified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="argv"/> is null.</exception>
    public static IReadOnlyList<string> AdaptToHost(IsolationPolicy policy, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(argv);
        var adapted = new List<string>(argv.Count);
        for (var i = 0; i < argv.Count; i++)
        {
            switch (argv[i])
            {
                case "--tmpfs" when i + 1 < argv.Count:
                    {
                        var target = argv[++i];
                        if (Directory.Exists(target))
                        {
                            adapted.Add("--tmpfs");
                            adapted.Add(target);
                        }
                        else if (File.Exists(target))
                        {
                            adapted.Add("--ro-bind");
                            adapted.Add(SocketMaskSource);
                            adapted.Add(target);
                        }

                        break;
                    }

                case "--bind" when i + 2 < argv.Count:
                    {
                        var source = argv[++i];
                        var destination = argv[++i];
                        if (!Directory.Exists(source))
                        {
                            throw new InvalidOperationException(
                                $"Isolation policy requires an existing directory for the write bind, but '{source}' does not exist on this host.");
                        }

                        adapted.Add("--bind");
                        adapted.Add(source);
                        adapted.Add(destination);
                        break;
                    }

                case "--ro-bind" when i + 2 < argv.Count:
                    {
                        var source = argv[++i];
                        var destination = argv[++i];
                        if (source == SocketMaskSource || Directory.Exists(source))
                        {
                            adapted.Add("--ro-bind");
                            adapted.Add(source);
                            adapted.Add(destination);
                        }

                        break;
                    }

                default:
                    adapted.Add(argv[i]);
                    break;
            }
        }

        return adapted;
    }

    private static void Bind(List<string> argv, string option, string path)
    {
        argv.Add(option);
        argv.Add(path);
        argv.Add(path);
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
