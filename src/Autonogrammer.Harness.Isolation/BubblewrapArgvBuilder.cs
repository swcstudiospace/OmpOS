namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Builds the bubblewrap argument vector. Mount order carries the precedence: the read-only root first, then the explicit
/// binds (read-write grants, scratch, state, then read-only grants), then the deny masks, so a deny nested under a grant
/// wins. An explicit bind nested under a deny target is emitted after that mask, so only an explicit grant reopens a
/// denied path. The root is never overlaid with a writable tmpfs, so a write outside a grant, scratch or state directory
/// is refused. A deny target that is a file (the docker socket paths) is masked with a read-only <c>/dev/null</c> bind
/// because tmpfs needs a directory. Every bind is a complete source and destination pair; a grant that cannot be
/// expressed as one is rejected with <see cref="MalformedArgvException"/>, never emitted. It does not start a process.
/// The environment scrub lives here so both OS wrappers apply the same names after the engine environment policy.
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
    /// <exception cref="MalformedArgvException">A grant, scratch, state or home path is not a complete absolute path.</exception>
    public static IReadOnlyList<string> Build(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var binds = ExplicitBinds(policy);
        var denies = DenyTargets(policy);
        var argv = new List<string>
        {
            "--ro-bind", "/", "/",
        };

        foreach (var bind in binds)
        {
            if (!IsUnderAny(bind.Target, denies))
            {
                AddBind(argv, bind);
            }
        }

        foreach (var denied in denies)
        {
            argv.Add("--tmpfs");
            argv.Add(denied);
        }

        foreach (var bind in binds)
        {
            if (IsUnderAny(bind.Target, denies))
            {
                AddBind(argv, bind);
            }
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
    /// Verifies that the canonical argument vector for <paramref name="policy"/> is complete and keeps its precedence:
    /// every read-write grant has a complete bind pair, every deny target and socket is masked, and each bind and mask
    /// that nest are ordered so the later argument wins. It checks argument shape only and does not look at the host.
    /// </summary>
    /// <param name="policy">The session policy to verify.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    /// <exception cref="MalformedArgvException">The policy does not produce a complete, correctly ordered argument vector.</exception>
    public static void Verify(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var mounts = ParseMounts(Build(policy));
        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite)
            {
                RequireMount(mounts, "--bind", grant.Path);
            }
        }

        foreach (var denied in DenyTargets(policy))
        {
            RequireMount(mounts, "--tmpfs", denied);
        }

        RequireDenyPrecedence(mounts);
    }

    /// <summary>
    /// Resolves a symlinked parent directory on this host (for example <c>/var/run</c> to <c>/run</c>), so a mask names
    /// the real path bubblewrap mounts on. A mount point reached through a symlink in the read-only root cannot be created.
    /// </summary>
    /// <param name="path">An absolute host path.</param>
    /// <returns>The path with its parent directory's symlink resolved; the input when the parent is not a link.</returns>
    private static string ResolveHostPath(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parent))
        {
            return path;
        }

        var resolved = new DirectoryInfo(parent).ResolveLinkTarget(returnFinalTarget: true);
        return resolved is null ? path : Path.Combine(resolved.FullName, Path.GetFileName(path));
    }

    /// <summary>
    /// Adapts the canonical argv to one host at launch time. Deny targets that do not exist are skipped (nothing to
    /// hide), a deny target that is a file becomes a <see cref="SocketMaskSource"/> bind (tmpfs needs a directory),
    /// and a read-only grant that does not exist is skipped because the read-only root already shows nothing. A
    /// read-write grant or scratch source that does not exist fails closed: the session cannot satisfy its own policy.
    /// A missing omp state directory (<c>~/.omp</c>, <c>PI_CODING_AGENT_DIR</c>) is created first, as omp itself does on its first run.
    /// </summary>
    /// <param name="policy">The same session policy <see cref="Build"/> received.</param>
    /// <param name="argv">The canonical argv to adapt.</param>
    /// <returns>A new argv list. The input is not modified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="argv"/> is null.</exception>
    /// <exception cref="MalformedArgvException">An option is unknown, or a bind or mask lacks a complete absolute operand.</exception>
    public static IReadOnlyList<string> AdaptToHost(IsolationPolicy policy, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(argv);
        _ = ParseMounts(argv);
        var adapted = new List<string>(argv.Count);
        for (var i = 0; i < argv.Count; i++)
        {
            switch (argv[i])
            {
                case "--tmpfs":
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
                            adapted.Add(ResolveHostPath(target));
                        }

                        break;
                    }

                case "--bind":
                    {
                        var source = argv[++i];
                        var destination = argv[++i];
                        EnsureStateDirectory(policy, source);
                        AdaptBind(adapted, source, destination);
                        break;
                    }

                case "--ro-bind":
                    {
                        var source = argv[++i];
                        var destination = argv[++i];
                        if (string.Equals(source, SocketMaskSource, StringComparison.Ordinal) || Directory.Exists(source))
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

    private static List<ExplicitBind> ExplicitBinds(IsolationPolicy policy)
    {
        var binds = new List<ExplicitBind>();
        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite)
            {
                binds.Add(new ExplicitBind("--bind", RequireAbsolute(grant.Path, "read-write grant")));
            }
        }

        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            binds.Add(new ExplicitBind("--bind", RequireAbsolute(policy.ScratchDirectory, "scratch directory")));
        }

        foreach (var state in policy.StateDirectories())
        {
            binds.Add(new ExplicitBind("--bind", RequireAbsolute(state, "state directory")));
        }

        foreach (var grant in policy.Grants)
        {
            if (!grant.ReadWrite)
            {
                binds.Add(new ExplicitBind("--ro-bind", RequireAbsolute(grant.Path, "read-only grant")));
            }
        }

        return binds;
    }

    private static List<string> DenyTargets(IsolationPolicy policy)
    {
        var denies = new List<string>();
        if (!string.IsNullOrWhiteSpace(policy.HomeDirectory))
        {
            denies.AddRange(IsolationDeniedDirectories.ForHome(RequireAbsolute(policy.HomeDirectory, "home directory")));
        }

        denies.AddRange(IsolationDeniedDirectories.SocketLocations);
        return denies;
    }

    private static string RequireAbsolute(string? path, string role)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.StartsWith('/')
            || path.Contains('\n', StringComparison.Ordinal)
            || path.Contains('\0', StringComparison.Ordinal)
            || path.Split('/').Contains(".."))
        {
            throw new MalformedArgvException(
                $"The {role} '{path}' must be a non-blank absolute path without '..' segments; the session cannot be confined as its policy states.");
        }

        return path;
    }

    private static void AddBind(List<string> argv, ExplicitBind bind)
    {
        argv.Add(bind.Option);
        argv.Add(bind.Target);
        argv.Add(bind.Target);
    }

    private static void EnsureStateDirectory(IsolationPolicy policy, string source)
    {
        if (!Directory.Exists(source) && policy.StateDirectories().Contains(source, StringComparer.Ordinal))
        {
            Directory.CreateDirectory(source);
        }
    }

    private static void AdaptBind(List<string> adapted, string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException(
                $"Isolation policy requires an existing directory for the write bind, but '{source}' does not exist on this host.");
        }

        adapted.Add("--bind");
        adapted.Add(source);
        adapted.Add(destination);
    }

    private static List<ParsedMount> ParseMounts(IReadOnlyList<string> argv)
    {
        var mounts = new List<ParsedMount>();
        for (var i = 0; i < argv.Count; i++)
        {
            var option = argv[i];
            switch (option)
            {
                case "--bind":
                case "--ro-bind":
                    mounts.Add(new ParsedMount(i, option, Operand(argv, i + 1, option), Operand(argv, i + 2, option)));
                    i += 2;
                    break;
                case "--tmpfs":
                    {
                        var target = Operand(argv, i + 1, option);
                        mounts.Add(new ParsedMount(i, option, target, target));
                        i += 1;
                        break;
                    }

                case "--dev":
                case "--proc":
                    _ = Operand(argv, i + 1, option);
                    i += 1;
                    break;
                case "--unshare-pid":
                case "--unshare-ipc":
                case "--unshare-uts":
                case "--die-with-parent":
                case "--new-session":
                    break;
                default:
                    throw new MalformedArgvException($"Unexpected bubblewrap argument '{option}' at position {i}.");
            }
        }

        return mounts;
    }

    private static string Operand(IReadOnlyList<string> argv, int index, string option)
    {
        if (index >= argv.Count || !argv[index].StartsWith('/'))
        {
            throw new MalformedArgvException(
                $"Bubblewrap option '{option}' is missing an absolute path operand at position {index}; the bind is incomplete.");
        }

        return argv[index];
    }

    private static void RequireMount(IReadOnlyList<ParsedMount> mounts, string option, string path)
    {
        var found = mounts.Any(mount =>
            string.Equals(mount.Option, option, StringComparison.Ordinal)
            && string.Equals(mount.Source, path, StringComparison.Ordinal)
            && string.Equals(mount.Destination, path, StringComparison.Ordinal));
        if (!found)
        {
            throw new MalformedArgvException($"'{path}' has no complete {option} pair in the argument vector.");
        }
    }

    private static void RequireDenyPrecedence(IReadOnlyList<ParsedMount> mounts)
    {
        foreach (var mask in mounts.Where(mount => string.Equals(mount.Option, "--tmpfs", StringComparison.Ordinal)))
        {
            foreach (var bind in mounts.Where(mount => !string.Equals(mount.Option, "--tmpfs", StringComparison.Ordinal)))
            {
                if (IsWithin(bind.Destination, mask.Destination) && bind.Index < mask.Index)
                {
                    throw new MalformedArgvException(
                        $"Bind '{bind.Destination}' is ordered before the deny mask '{mask.Destination}' that hides it, so the grant would be lost.");
                }

                if (IsStrictlyWithin(mask.Destination, bind.Destination) && mask.Index < bind.Index)
                {
                    throw new MalformedArgvException(
                        $"Deny mask '{mask.Destination}' is ordered before the bind '{bind.Destination}' that would re-expose it.");
                }
            }
        }
    }

    private static bool IsUnderAny(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            if (IsWithin(path, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(string child, string parent)
    {
        var normalizedChild = child.TrimEnd('/');
        var normalizedParent = parent.TrimEnd('/');
        return string.Equals(normalizedChild, normalizedParent, StringComparison.Ordinal)
            || normalizedChild.StartsWith(normalizedParent + "/", StringComparison.Ordinal);
    }

    private static bool IsStrictlyWithin(string child, string parent) =>
        IsWithin(child, parent) && !string.Equals(child.TrimEnd('/'), parent.TrimEnd('/'), StringComparison.Ordinal);

    private sealed record ExplicitBind(string Option, string Target);

    private sealed record ParsedMount(int Index, string Option, string Source, string Destination);
}
