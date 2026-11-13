namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Builds the bubblewrap argument vector. Mount order carries the precedence: the read-only root, then a fresh
/// <c>/dev</c> and <c>/proc</c>, then a fresh writable <c>/tmp</c> tmpfs (never the host <c>/tmp</c>), then the explicit binds sorted by destination depth so an ancestor always mounts before
/// its descendant (a read-only ancestor before a writable descendant, and the reverse), then the deny masks, and last the
/// explicit binds that sit under a deny, so only an explicit grant reopens a denied path. The root is never overlaid with
/// a writable tmpfs except <c>/tmp</c>, so a write outside a grant, scratch, state directory or <c>/tmp</c> is refused. A deny target that is a file (the
/// docker socket paths) is masked with a read-only <c>/dev/null</c> bind because tmpfs needs a directory. Every bind is a
/// complete source and destination pair; a grant that cannot be expressed as one, or that names the root, <c>/home</c>,
/// an ancestor of the home directory, or a system mount, is rejected with <see cref="MalformedArgvException"/>, never
/// emitted. It does not start a process. The environment scrub lives here so both OS wrappers apply the same names after
/// the engine environment policy.
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

    private static readonly string[] SystemMountRoots = ["/dev", "/proc", "/sys"];

    /// <summary>The sandbox's writable tmpfs. It is always a fresh tmpfs, never a bind of the host directory.</summary>
    private const string TmpPath = "/tmp";

    /// <summary>Builds the canonical confinement arguments for <paramref name="policy"/>, without the intended command.</summary>
    /// <param name="policy">The session policy. Binds are sorted by depth; a deny mask follows every bind outside the deny.</param>
    /// <returns>The argument vector, one element each, in the golden order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    /// <exception cref="MalformedArgvException">A grant, scratch, state or home path is not a complete absolute path, or names a forbidden mount.</exception>
    public static IReadOnlyList<string> Build(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var binds = ExplicitBinds(policy);
        var denies = DenyTargets(policy);
        var argv = new List<string>
        {
            "--ro-bind", "/", "/",
            "--dev", "/dev",
            "--proc", "/proc",
            "--tmpfs", TmpPath,
        };

        foreach (var bind in ByDepth(binds.Where(candidate => !IsUnderAny(candidate.Target, denies))))
        {
            AddBind(argv, bind);
        }

        foreach (var denied in denies)
        {
            argv.Add("--tmpfs");
            argv.Add(denied);
        }

        foreach (var bind in ByDepth(binds.Where(candidate => IsUnderAny(candidate.Target, denies))))
        {
            AddBind(argv, bind);
        }

        argv.Add("--unshare-pid");
        argv.Add("--unshare-ipc");
        argv.Add("--unshare-uts");
        argv.Add("--die-with-parent");
        argv.Add("--new-session");
        return argv;
    }

    /// <summary>
    /// Verifies that the argument vector that will launch for <paramref name="policy"/> is complete and keeps its
    /// precedence. The vector checked is <see cref="AdaptToHost(IsolationPolicy, IReadOnlyList{string})"/> applied to
    /// <see cref="Build"/>, computed without creating any missing state directory, so the check has no side effect and
    /// the launched vector is the same one. Every read-write grant has a complete bind pair; every deny target present on
    /// this host is masked; each ancestor bind precedes its descendants; and no bind that sits under a deny precedes it.
    /// </summary>
    /// <param name="policy">The session policy to verify.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    /// <exception cref="MalformedArgvException">The policy does not produce a complete, correctly ordered argument vector on this host.</exception>
    public static void Verify(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        VerifyAdapted(policy, AdaptForVerification(policy));
    }

    /// <summary>Verifies one argument vector already adapted to the host for <paramref name="policy"/>. This is the vector a launch passes to bubblewrap.</summary>
    /// <param name="policy">The session policy the vector was adapted from.</param>
    /// <param name="adapted">The host-adapted argument vector.</param>
    /// <exception cref="MalformedArgvException">The vector omits a grant or mask, or orders a bind against its ancestor or its deny.</exception>
    internal static void VerifyAdapted(IsolationPolicy policy, IReadOnlyList<string> adapted)
    {
        var mounts = ParseMounts(adapted);
        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite)
            {
                RequireMount(mounts, "--bind", grant.Path);
            }
        }

        foreach (var denied in DenyTargets(policy))
        {
            if (IsPresent(denied))
            {
                RequireMask(mounts, ResolveHostPath(denied));
            }
        }

        RequireAncestorOrder(mounts);
        RequireDenyPrecedence(mounts);
        RequireTmpfsTmp(mounts);
        RequireNoHostTmpBind(policy, mounts);
    }

    /// <summary>The deny targets for <paramref name="policy"/> in canonical order, for the session canary to check from inside the sandbox.</summary>
    /// <param name="policy">The session policy.</param>
    /// <returns>Absolute paths, one per deny target.</returns>
    internal static IReadOnlyList<string> MaskTargets(IsolationPolicy policy) => DenyTargets(policy);

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
    /// hide), a directory deny becomes a tmpfs on its resolved path, a deny target that is a file becomes a
    /// <see cref="SocketMaskSource"/> bind (tmpfs needs a directory), and a read-only grant that does not exist is skipped
    /// because the read-only root already shows nothing. A read-write grant or scratch source that does not exist fails
    /// closed. A missing omp state directory (<c>~/.omp</c>, <c>PI_CODING_AGENT_DIR</c>) is created first, as omp itself does on its first run.
    /// </summary>
    /// <param name="policy">The same session policy <see cref="Build"/> received.</param>
    /// <param name="argv">The canonical argv to adapt.</param>
    /// <returns>A new argv list. The input is not modified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> or <paramref name="argv"/> is null.</exception>
    /// <exception cref="MalformedArgvException">An option is unknown, or a bind or mask lacks a complete absolute operand.</exception>
    /// <exception cref="InvalidOperationException">A read-write bind source is missing on this host.</exception>
    public static IReadOnlyList<string> AdaptToHost(IsolationPolicy policy, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(argv);
        return Adapt(policy, argv, createStateDirectories: true);
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

    private static List<string> AdaptForVerification(IsolationPolicy policy)
    {
        try
        {
            return Adapt(policy, Build(policy), createStateDirectories: false);
        }
        catch (InvalidOperationException exception) when (exception is not MalformedArgvException)
        {
            throw new MalformedArgvException(exception.Message, exception);
        }
    }

    private static List<string> Adapt(IsolationPolicy policy, IReadOnlyList<string> argv, bool createStateDirectories)
    {
        _ = ParseMounts(argv);
        var adapted = new List<string>(argv.Count);
        for (var i = 0; i < argv.Count; i++)
        {
            switch (argv[i])
            {
                case "--tmpfs":
                    AdaptDenyMask(adapted, argv[++i]);
                    break;

                case "--bind":
                    {
                        var source = argv[++i];
                        var destination = argv[++i];
                        AdaptBind(policy, adapted, source, destination, createStateDirectories);
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

    private static void AdaptDenyMask(List<string> adapted, string target)
    {
        if (Directory.Exists(target))
        {
            adapted.Add("--tmpfs");
            adapted.Add(ResolveHostPath(target));
        }
        else if (File.Exists(target))
        {
            adapted.Add("--ro-bind");
            adapted.Add(SocketMaskSource);
            adapted.Add(ResolveHostPath(target));
        }
    }

    private static void AdaptBind(IsolationPolicy policy, List<string> adapted, string source, string destination, bool createStateDirectories)
    {
        if (!Directory.Exists(source))
        {
            if (!IsStateDirectory(policy, source))
            {
                throw new InvalidOperationException(
                    $"Isolation policy requires an existing directory for the write bind, but '{source}' does not exist on this host.");
            }

            if (createStateDirectories)
            {
                Directory.CreateDirectory(source);
            }
        }

        adapted.Add("--bind");
        adapted.Add(source);
        adapted.Add(destination);
    }

    private static bool IsStateDirectory(IsolationPolicy policy, string source) =>
        policy.StateDirectories().Contains(source, StringComparer.Ordinal);

    private static bool IsPresent(string path) => Directory.Exists(path) || File.Exists(path);

    private static List<ExplicitBind> ExplicitBinds(IsolationPolicy policy)
    {
        var binds = new List<ExplicitBind>();
        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite)
            {
                binds.Add(new ExplicitBind("--bind", RequireBindable(policy, grant.Path, "read-write grant")));
            }
        }

        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            binds.Add(new ExplicitBind("--bind", RequireBindable(policy, policy.ScratchDirectory, "scratch directory")));
        }

        foreach (var state in policy.StateDirectories())
        {
            binds.Add(new ExplicitBind("--bind", RequireBindable(policy, state, "state directory")));
        }

        foreach (var grant in policy.Grants)
        {
            if (!grant.ReadWrite)
            {
                binds.Add(new ExplicitBind("--ro-bind", RequireBindable(policy, grant.Path, "read-only grant")));
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

    private static IEnumerable<ExplicitBind> ByDepth(IEnumerable<ExplicitBind> binds) =>
        binds.OrderBy(bind => Depth(bind.Target));

    private static int Depth(string path) => path.TrimEnd('/').Count(character => character == '/');

    private static string RequireBindable(IsolationPolicy policy, string? path, string role)
    {
        var absolute = RequireAbsolute(path, role);
        var normalized = absolute.TrimEnd('/');
        var isRoot = normalized.Length == 0;
        var isHome = string.Equals(normalized, "/home", StringComparison.Ordinal);
        var isHomeAncestor = !string.IsNullOrWhiteSpace(policy.HomeDirectory) && IsStrictlyWithin(policy.HomeDirectory, absolute);
        if (isRoot || isHome || isHomeAncestor || IsUnderAny(absolute, SystemMountRoots))
        {
            throw new MalformedArgvException(
                $"The {role} '{absolute}' is the root, /home, an ancestor of the home directory, or a system mount and cannot be bound; the session cannot be confined as its policy states.");
        }

        return absolute;
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

    private static bool IsMask(ParsedMount mount) =>
        string.Equals(mount.Option, "--tmpfs", StringComparison.Ordinal)
        || (string.Equals(mount.Option, "--ro-bind", StringComparison.Ordinal)
            && string.Equals(mount.Source, SocketMaskSource, StringComparison.Ordinal));

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

    private static void RequireMask(IReadOnlyList<ParsedMount> mounts, string destination)
    {
        var found = mounts.Any(mount =>
            IsMask(mount) && string.Equals(mount.Destination, destination, StringComparison.Ordinal));
        if (!found)
        {
            throw new MalformedArgvException(
                $"'{destination}' is not masked by a --tmpfs directory or a read-only /dev/null bind in the argument vector.");
        }
    }

    private static void RequireAncestorOrder(IReadOnlyList<ParsedMount> mounts)
    {
        var binds = mounts.Where(mount => !IsMask(mount)).ToList();
        foreach (var ancestor in binds)
        {
            foreach (var descendant in binds)
            {
                if (IsStrictlyWithin(descendant.Destination, ancestor.Destination) && descendant.Index < ancestor.Index)
                {
                    throw new MalformedArgvException(
                        $"Bind '{descendant.Destination}' is ordered before its ancestor bind '{ancestor.Destination}', so the ancestor would hide the descendant.");
                }
            }
        }
    }

    private static void RequireDenyPrecedence(IReadOnlyList<ParsedMount> mounts)
    {
        var binds = mounts.Where(mount => !IsMask(mount)).ToList();
        foreach (var mask in mounts.Where(IsMask))
        {
            foreach (var bind in binds)
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

    private static void RequireTmpfsTmp(IReadOnlyList<ParsedMount> mounts)
    {
        var count = mounts.Count(mount =>
            string.Equals(mount.Option, "--tmpfs", StringComparison.Ordinal)
            && string.Equals(mount.Destination, TmpPath, StringComparison.Ordinal));
        if (count != 1)
        {
            throw new MalformedArgvException(
                $"'{TmpPath}' must be mounted by exactly one fresh --tmpfs in the argument vector; found {count}.");
        }
    }

    private static void RequireNoHostTmpBind(IsolationPolicy policy, IReadOnlyList<ParsedMount> mounts)
    {
        var named = NamedBindPaths(policy);
        foreach (var bind in mounts.Where(candidate => !IsMask(candidate)))
        {
            var touchesTmp = IsWithin(bind.Source, TmpPath) || IsWithin(bind.Destination, TmpPath);
            var isNamedGrant = string.Equals(bind.Source, bind.Destination, StringComparison.Ordinal)
                && named.Contains(bind.Source, StringComparer.Ordinal);
            if (touchesTmp && !isNamedGrant)
            {
                throw new MalformedArgvException(
                    $"Bind '{bind.Source}' to '{bind.Destination}' touches {TmpPath}, which may only be the fresh tmpfs or an explicit grant the policy names; the host {TmpPath} must not be bound.");
            }
        }
    }

    private static List<string> NamedBindPaths(IsolationPolicy policy)
    {
        var paths = policy.Grants.Select(grant => grant.Path).ToList();
        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            paths.Add(policy.ScratchDirectory);
        }

        paths.AddRange(policy.StateDirectories());
        return paths;
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
