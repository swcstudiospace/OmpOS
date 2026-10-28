namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>
/// The decision engine: a tool call, the run mode, the permission rules and the read boundary in, <see cref="RunModeDecision"/> out.
/// The order is fixed. A hook may refuse a call before this runs. Then a deny rule refuses it. In <see cref="RunMode.RunEverything"/> every other
/// call is allowed. Otherwise an allow rule allows it, Auto-review also allows a known-safe read-only command, and the call asks unless the
/// rules that apply to its kind allow it: shell commands, MCP tools and fetches ask unless an allow rule matches; reads ask outside the read boundary;
/// writes ask outside the workspace and on protected paths. There is no classifier: what Cursor would send to it asks you here.
/// </summary>
/// <remarks>Only the calls omp sends to the client reach this engine. In <see cref="RunMode.RunEverything"/> omp sends none, so no rule is consulted for them.</remarks>
public sealed class RunModePolicy
{
    private static readonly string[] ProtectedWorkspacePatterns =
    [
        ".ompos/*.json", ".ompos/**/*.json", ".cursor/*.json", ".cursor/**/*.json", ".cursor/.workspace-trusted", ".claude/*.json", ".claude/**/*.json",
        ".vscode/**", "*.code-workspace", ".git/hooks/**", ".git/config", ".git/info/attributes", ".cursorignore",
    ];

    private static readonly string[] ProtectedHomePatterns = ["~/.ompos/**/*.json", "~/.cursor/**/*.json"];

    private readonly EffectivePermissions _permissions;
    private readonly SafetyPaths _paths;
    private readonly SandboxBoundary _boundary;
    private readonly List<SafetyPatterns.Resolved> _protected;

    /// <summary>Initializes a new instance of the <see cref="RunModePolicy"/> class.</summary>
    /// <param name="mode">The run mode.</param>
    /// <param name="permissions">The merged permission files.</param>
    /// <param name="sandbox">The read boundary in force.</param>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <param name="homeDirectory">The home directory <c>~</c> expands to, or null.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="homeDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
    public RunModePolicy(RunMode mode, EffectivePermissions permissions, EffectiveSandbox sandbox, string workspaceRoot, string? homeDirectory)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(sandbox);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown run mode.");
        }

        Mode = mode;
        _permissions = permissions;
        _paths = new SafetyPaths(workspaceRoot, homeDirectory);
        _boundary = new SandboxBoundary(sandbox, _paths);
        _protected = [.. ProtectedWorkspacePatterns.Select(pattern => SafetyPatterns.Resolve(_paths, pattern))];
        if (_paths.Home is not null)
        {
            _protected.AddRange(ProtectedHomePatterns.Select(pattern => SafetyPatterns.Resolve(_paths, pattern)));
        }
    }

    /// <summary>Gets the run mode.</summary>
    public RunMode Mode { get; }

    /// <summary>Decides one call. It never throws for a well-formed request.</summary>
    /// <param name="request">The call.</param>
    /// <returns>The decision and its audit record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public RunModeDecision Decide(RunModeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (FindDeny(request) is { } denied)
        {
            return new RunModeDecision(RunModeOutcome.Deny, RunModeStage.DenyRule, $"{denied.Token.Text} in {denied.Source} refuses this call.", denied);
        }

        if (Mode == RunMode.RunEverything)
        {
            return new RunModeDecision(RunModeOutcome.Allow, RunModeStage.Mode, "Run Everything runs every call.");
        }

        return request.Kind switch
        {
            RunModeToolKind.Shell => DecideShell(request),
            RunModeToolKind.Read => DecideRead(request),
            RunModeToolKind.Write => DecideWrite(request),
            RunModeToolKind.Fetch => DecideFetch(request),
            RunModeToolKind.Mcp => DecideMcp(request),
            _ => Ask(RunModeStage.UnknownRequest, "OmpOS does not recognise this kind of call, so you decide."),
        };
    }

    private static RunModeDecision Ask(RunModeStage stage, string reason) => new(RunModeOutcome.Ask, stage, reason);

    private static RunModeDecision Allow(RunModeStage stage, string reason, PermissionRule? rule = null) => new(RunModeOutcome.Allow, stage, reason, rule);

    private PermissionRule? FindDeny(RunModeRequest request)
    {
        switch (request.Kind)
        {
            case RunModeToolKind.Shell when !string.IsNullOrWhiteSpace(request.Command):
                var candidates = ShellCommandAnalysis.CandidatesForDeny(request.Command);
                return Rules(_permissions.DenyRules, PermissionTokenKind.Shell)
                    .FirstOrDefault(rule => candidates.Any(words => PermissionMatcher.MatchesShell(rule.Token.Argument, words, forDeny: true)));
            case RunModeToolKind.Read or RunModeToolKind.Write:
                var kind = request.Kind == RunModeToolKind.Read ? PermissionTokenKind.Read : PermissionTokenKind.Write;
                foreach (var path in request.Paths ?? [])
                {
                    var lexical = _paths.Lexical(path, request.WorkingDirectory);
                    var real = _paths.Real(path, request.WorkingDirectory);
                    var match = Rules(_permissions.DenyRules, kind).FirstOrDefault(rule => PermissionMatcher.MatchesPath(rule.Token.Argument, _paths, lexical, real, forDeny: true));
                    if (match is not null)
                    {
                        return match;
                    }
                }

                return null;
            case RunModeToolKind.Fetch when PermissionMatcher.HostOf(request.FetchAddress) is { } host:
                return Rules(_permissions.DenyRules, PermissionTokenKind.WebFetch).FirstOrDefault(rule => PermissionMatcher.MatchesHost(rule.Token.Argument, host));
            case RunModeToolKind.Mcp when request.McpServer is { Length: > 0 } server && request.McpTool is { Length: > 0 } tool:
                return Rules(_permissions.DenyRules, PermissionTokenKind.Mcp).FirstOrDefault(rule => PermissionMatcher.MatchesMcp(rule.Token.Argument, server, tool));
            default:
                return null;
        }
    }

    private static IEnumerable<PermissionRule> Rules(IReadOnlyList<PermissionRule> rules, PermissionTokenKind kind) => rules.Where(rule => rule.Token.Kind == kind);

    private RunModeDecision DecideShell(RunModeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return Ask(RunModeStage.UnknownRequest, "The command line is not known, so you decide.");
        }

        var reading = ShellCommandAnalysis.ReadForAllow(request.Command);
        if (!reading.Opaque && reading.Segments.Count > 0)
        {
            var shellRules = Rules(_permissions.AllowRules, PermissionTokenKind.Shell).ToList();
            PermissionRule? first = null;
            var all = true;
            foreach (var words in reading.Segments)
            {
                var rule = ShellCommandAnalysis.IsAssignment(words[0])
                    ? null
                    : shellRules.Find(candidate => PermissionMatcher.MatchesShell(candidate.Token.Argument, words, forDeny: false));
                if (rule is null)
                {
                    all = false;
                    break;
                }

                first ??= rule;
            }

            if (all && first is not null)
            {
                return Allow(RunModeStage.AllowRule, $"{first.Token.Text} in {first.Source} allows this command.", first);
            }
        }

        if (Mode == RunMode.AutoReview
            && (request.WorkingDirectory is null || ArgumentReadable(request.WorkingDirectory, null))
            && KnownSafeCommands.IsKnownSafe(reading, argument => ArgumentReadable(argument, request.WorkingDirectory)))
        {
            return Allow(RunModeStage.KnownSafe, "A read-only command from the known-safe list, inside the read boundary.");
        }

        return Ask(
            RunModeStage.Default,
            Mode == RunMode.AutoReview
                ? "No allow rule matches. OmpOS has no Auto-review classifier, so you decide."
                : "No allow rule matches, so you decide.");
    }

    private bool ArgumentReadable(string argument, string? workingDirectory)
    {
        var real = _paths.Real(argument, workingDirectory);
        if (!_boundary.IsReadable(real))
        {
            return false;
        }

        var lexical = _paths.Lexical(argument, workingDirectory);
        return !Rules(_permissions.DenyRules, PermissionTokenKind.Read).Any(rule => PermissionMatcher.MatchesPath(rule.Token.Argument, _paths, lexical, real, forDeny: true));
    }

    private RunModeDecision DecideRead(RunModeRequest request)
    {
        if (request.Paths is not { Count: > 0 })
        {
            return Ask(RunModeStage.UnknownRequest, "The call names no file, so you decide.");
        }

        PermissionRule? rule = null;
        foreach (var path in request.Paths)
        {
            var lexical = _paths.Lexical(path, request.WorkingDirectory);
            var real = _paths.Real(path, request.WorkingDirectory);
            var allowRule = Rules(_permissions.AllowRules, PermissionTokenKind.Read).FirstOrDefault(candidate => PermissionMatcher.MatchesPath(candidate.Token.Argument, _paths, lexical, real, forDeny: false));
            if (allowRule is not null)
            {
                rule ??= allowRule;
                continue;
            }

            if (!_boundary.IsReadable(real))
            {
                return Ask(RunModeStage.ReadBoundary, $"'{path}' is outside the workspace read boundary, so you decide.");
            }
        }

        return rule is not null
            ? Allow(RunModeStage.AllowRule, $"{rule.Token.Text} in {rule.Source} allows this read.", rule)
            : Allow(RunModeStage.ReadBoundary, _boundary.ReadBoundary == SandboxReadBoundary.System ? "Reads are allowed anywhere (read boundary: system)." : "Every file is inside the workspace read boundary.");
    }

    private RunModeDecision DecideWrite(RunModeRequest request)
    {
        if (request.Paths is not { Count: > 0 })
        {
            return Ask(RunModeStage.UnknownRequest, "The call names no file, so you decide.");
        }

        PermissionRule? rule = null;
        foreach (var path in request.Paths)
        {
            var lexical = _paths.Lexical(path, request.WorkingDirectory);
            var real = _paths.Real(path, request.WorkingDirectory);
            if (IsProtected(lexical, real))
            {
                return Ask(RunModeStage.ProtectedPath, $"'{path}' is a protected path (OmpOS or editor settings, git hooks), so you decide.");
            }

            if (_paths.IsInWorkspace(real))
            {
                continue;
            }

            var allowRule = Rules(_permissions.AllowRules, PermissionTokenKind.Write).FirstOrDefault(candidate => PermissionMatcher.MatchesPath(candidate.Token.Argument, _paths, lexical, real, forDeny: false));
            if (allowRule is null)
            {
                return Ask(RunModeStage.WriteScope, $"'{path}' is outside the workspace, so you decide.");
            }

            rule ??= allowRule;
        }

        return rule is not null
            ? Allow(RunModeStage.AllowRule, $"{rule.Token.Text} in {rule.Source} allows this write.", rule)
            : Allow(RunModeStage.WriteScope, "Every file is inside the workspace and none is protected.");
    }

    private bool IsProtected(string lexical, string? real) =>
        _protected.Exists(pattern =>
            GlobMatcher.MatchPath(pattern.Lexical, lexical, _paths.IgnoreCase)
            || (real is not null && pattern.Real is not null && GlobMatcher.MatchPath(pattern.Real, real, _paths.IgnoreCase)));

    private RunModeDecision DecideFetch(RunModeRequest request)
    {
        if (PermissionMatcher.HostOf(request.FetchAddress) is not { } host)
        {
            return Ask(RunModeStage.UnknownRequest, "The address is not known, so you decide.");
        }

        var rule = Rules(_permissions.AllowRules, PermissionTokenKind.WebFetch).FirstOrDefault(candidate => PermissionMatcher.MatchesHost(candidate.Token.Argument, host));
        return rule is not null
            ? Allow(RunModeStage.AllowRule, $"{rule.Token.Text} in {rule.Source} allows this fetch.", rule)
            : Ask(RunModeStage.Default, $"No WebFetch rule allows {host}, so you decide.");
    }

    private RunModeDecision DecideMcp(RunModeRequest request)
    {
        if (request.McpServer is not { Length: > 0 } server || request.McpTool is not { Length: > 0 } tool)
        {
            return Ask(RunModeStage.UnknownRequest, "The MCP server or tool is not known, so you decide.");
        }

        var rule = Rules(_permissions.AllowRules, PermissionTokenKind.Mcp).FirstOrDefault(candidate => PermissionMatcher.MatchesMcp(candidate.Token.Argument, server, tool));
        return rule is not null
            ? Allow(RunModeStage.AllowRule, $"{rule.Token.Text} in {rule.Source} allows this tool.", rule)
            : Ask(RunModeStage.Default, $"No MCP rule allows {server}:{tool}, so you decide.");
    }
}
