using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Safety;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// The permission seam for a Cowork omp child. It denies a path outside the trusted folders and the Storage folder,
/// always asks before a delete, allows <c>mcp__cowork_*</c> at the omp level, and otherwise follows the mode.
/// Auto runs an allow rule or a short read-only known-safe command inside the trusted folders, refuses a deny rule, and asks otherwise.
/// Tool-provider and MCP calls follow Manual. A tainted session follows Manual for every call. Decisions are recorded when an audit sink is supplied.
/// </summary>
public sealed class CoworkPermissionResolver : IPermissionResolver
{
    private static readonly HashSet<string> TaintedActuatingKinds = new(StringComparer.Ordinal) { "edit", "delete", "move", "execute" };
    private static readonly HashSet<string> PathKinds = new(StringComparer.Ordinal) { "read", "edit", "delete", "move", "search", "execute" };
    private static readonly HashSet<string> WriteKinds = new(StringComparer.Ordinal) { "edit", "delete", "move", "execute" };
    private static readonly HashSet<string> AlwaysAllowKinds = new(StringComparer.Ordinal) { "think", "switch_mode" };

    private readonly IPermissionResolver _askUser;
    private readonly CoworkPathBoundary _boundary;
    private readonly IReadOnlyList<string> _trustedFolders;
    private readonly string? _storageFolder;
    private readonly string _workingDirectory;
    private readonly Tools.CoworkPermissionMode _requestedMode;
    private readonly Tools.CoworkPermissionMode _effectiveMode;
    private readonly Func<bool> _isTainted;
    private readonly HashSet<string> _catalogServers;
    private readonly IRunModeAuditSink? _audit;
    private readonly string? _permissionsHome;
    private readonly string? _permissionsProject;
    private readonly IReadOnlyList<CoworkFolderGrant>? _folderGrants;

    /// <summary>Initializes a new instance of the <see cref="CoworkPermissionResolver"/> class.</summary>
    /// <param name="askUser">The resolver that asks the user. It receives every request this policy cannot settle.</param>
    /// <param name="boundary">The folder boundary.</param>
    /// <param name="trustedFolders">The trusted folders.</param>
    /// <param name="storageFolder">The Storage folder, or null.</param>
    /// <param name="workingDirectory">The directory a relative path is resolved against.</param>
    /// <param name="mode">The stored mode.</param>
    /// <param name="isTainted">Whether the session is tainted. Called for every request.</param>
    /// <param name="filesystemIsolated">Whether the folder boundary is FilesystemIsolated.</param>
    /// <param name="skipAcknowledged">Whether the user acknowledged Skip for this session.</param>
    /// <param name="catalogServers">Catalog MCP server names. They ask in a tainted session even in Skip.</param>
    /// <param name="audit">The audit log, or null when decisions are not recorded.</param>
    /// <param name="permissionsHome">The home directory whose permission files Auto and Manual read, or null.</param>
    /// <param name="permissionsProject">The workspace whose permission files are read, or null.</param>
    /// <param name="folderGrants">Folder grants with access mode. Null keeps the previous trusted-folder write behaviour.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public CoworkPermissionResolver(
        IPermissionResolver askUser,
        CoworkPathBoundary boundary,
        IReadOnlyList<string> trustedFolders,
        string? storageFolder,
        string workingDirectory,
        Tools.CoworkPermissionMode mode,
        Func<bool> isTainted,
        bool filesystemIsolated = false,
        bool skipAcknowledged = false,
        IReadOnlyCollection<string>? catalogServers = null,
        IRunModeAuditSink? audit = null,
        string? permissionsHome = null,
        string? permissionsProject = null,
        IReadOnlyList<CoworkFolderGrant>? folderGrants = null)
    {
        ArgumentNullException.ThrowIfNull(askUser);
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(trustedFolders);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(isTainted);
        _askUser = askUser;
        _boundary = boundary;
        _trustedFolders = trustedFolders;
        _storageFolder = storageFolder;
        _workingDirectory = workingDirectory;
        _requestedMode = mode;
        _effectiveMode = CoworkPermissionMode.Effective(mode, filesystemIsolated, skipAcknowledged);
        _isTainted = isTainted;
        _catalogServers = new HashSet<string>(catalogServers ?? [], StringComparer.Ordinal);
        _audit = audit;
        _permissionsHome = permissionsHome;
        _permissionsProject = permissionsProject;
        _folderGrants = folderGrants;
        SkipUnavailableReason = mode == Tools.CoworkPermissionMode.Skip && _effectiveMode != Tools.CoworkPermissionMode.Skip
            ? CoworkPermissionMode.SkipUnavailableReason
            : null;
    }

    /// <summary>Why the picker must say Skip is unavailable, or null when Skip is not requested or is available.</summary>
    public string? SkipUnavailableReason { get; }

    /// <inheritdoc />
    public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parsed = RunModeRequestParser.Parse(request);
        var kind = RunModeRequestParser.AcpKind(request);
        if (IsCoworkTool(request.Name) || IsCoworkTool(parsed.ToolName) || string.Equals(parsed.McpServer, "cowork", StringComparison.Ordinal))
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.CoworkAutoRule, "Cowork tools are allowed at the omp level.", cancellationToken);
        }

        if (AlwaysAllowKinds.Contains(kind))
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.CoworkAutoRule, "This kind does not act on files.", cancellationToken);
        }

        if (Tainted && _effectiveMode == Tools.CoworkPermissionMode.Auto)
        {
            return AskAsync(request, parsed, RunModeStage.CoworkTaintDemotion, "A tainted session follows Manual for every call.", cancellationToken);
        }

        if (parsed.Kind == RunModeToolKind.Mcp)
        {
            return DecideMcpAsync(request, parsed, cancellationToken);
        }

        if (PathKinds.Contains(kind))
        {
            if (!PathsAllowed(parsed, kind))
            {
                return FinishAsync(request, parsed, PermissionDecision.Deny, RunModeStage.CoworkFolderBoundary, "The path is outside the trusted folders.", cancellationToken);
            }

            if (_folderGrants is not null && WriteKinds.Contains(kind) && !WritesAllowed(parsed, kind))
            {
                return FinishAsync(request, parsed, PermissionDecision.Deny, RunModeStage.CoworkFolderBoundary, "A read-only grant refuses writes.", cancellationToken);
            }

            if (IsDeleting(kind, parsed))
            {
                return AskAsync(request, parsed, RunModeStage.CoworkDeleteProtection, "Deletion protection asks in every Cowork mode.", cancellationToken);
            }

            if (Tainted && TaintedActuatingKinds.Contains(kind))
            {
                return AskAsync(request, parsed, RunModeStage.CoworkTaintDemotion, "A tainted session asks before this call.", cancellationToken);
            }
        }
        else if (IsDeleting(kind, parsed))
        {
            return AskAsync(request, parsed, RunModeStage.CoworkDeleteProtection, "Deletion protection asks in every Cowork mode.", cancellationToken);
        }

        if (_effectiveMode == Tools.CoworkPermissionMode.Auto && parsed.Command is { Length: > 0 } command)
        {
            return DecideAutoShellAsync(request, parsed, command, cancellationToken);
        }

        return FollowModeAsync(request, parsed, cancellationToken);
    }

    private bool Tainted => _isTainted();

    private Task<PermissionDecision> DecideMcpAsync(PermissionRequestEvent request, RunModeRequest parsed, CancellationToken cancellationToken)
    {
        var permissions = PermissionsLoader.Load(_permissionsHome, _permissionsProject);
        var server = parsed.McpServer ?? string.Empty;
        var tool = parsed.McpTool ?? string.Empty;
        var denied = MatchMcp(permissions.DenyRules, server, tool);
        if (denied is not null)
        {
            return FinishAsync(request, parsed, PermissionDecision.Deny, RunModeStage.CoworkAutoRule, "A Blocked MCP rule refuses this call.", cancellationToken, denied);
        }

        if (Tainted && _catalogServers.Contains(server))
        {
            return AskAsync(request, parsed, RunModeStage.CoworkTaintDemotion, "A tainted session asks before a catalog MCP call.", cancellationToken);
        }

        var allowed = MatchMcp(permissions.AllowRules, server, tool);
        if (allowed is not null && _effectiveMode is Tools.CoworkPermissionMode.Manual or Tools.CoworkPermissionMode.Auto)
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.CoworkAutoRule, "An MCP allow rule runs this call.", cancellationToken, allowed);
        }

        if (_effectiveMode == Tools.CoworkPermissionMode.Skip && _catalogServers.Contains(server))
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.Mode, "Skip allows a catalog MCP call that is not denied.", cancellationToken);
        }

        return AskAsync(request, parsed, StageForAsk(), "MCP calls follow Manual when no allow rule matches.", cancellationToken);
    }

    private Task<PermissionDecision> DecideAutoShellAsync(PermissionRequestEvent request, RunModeRequest parsed, string command, CancellationToken cancellationToken)
    {
        var permissions = PermissionsLoader.Load(_permissionsHome, _permissionsProject);
        var denied = MatchShell(permissions.DenyRules, command, forDeny: true);
        if (denied is not null)
        {
            return FinishAsync(request, parsed, PermissionDecision.Deny, RunModeStage.CoworkAutoRule, "A deny rule refuses this command.", cancellationToken, denied);
        }

        var allowed = MatchShell(permissions.AllowRules, command, forDeny: false);
        if (allowed is not null)
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.CoworkAutoRule, "An allow rule runs this command.", cancellationToken, allowed);
        }

        if (KnownSafeShell.IsKnownSafe(command, ArgumentAllowed))
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.CoworkAutoRule, "A read-only known-safe command inside the trusted folders.", cancellationToken);
        }

        return AskAsync(request, parsed, RunModeStage.CoworkAutoRule, "No allow rule matches, so Auto asks.", cancellationToken);
    }

    private Task<PermissionDecision> FollowModeAsync(PermissionRequestEvent request, RunModeRequest parsed, CancellationToken cancellationToken)
    {
        if (_effectiveMode == Tools.CoworkPermissionMode.Skip)
        {
            return FinishAsync(request, parsed, PermissionDecision.Allow, RunModeStage.Mode, "Skip allows this call.", cancellationToken);
        }

        return AskAsync(request, parsed, StageForAsk(), "The mode asks.", cancellationToken);
    }

    private RunModeStage StageForAsk() =>
        _requestedMode == Tools.CoworkPermissionMode.Skip && _effectiveMode != Tools.CoworkPermissionMode.Skip
            ? RunModeStage.CoworkSkipGate
            : RunModeStage.Default;

    private Task<PermissionDecision> AskAsync(PermissionRequestEvent request, RunModeRequest parsed, RunModeStage stage, string reason, CancellationToken cancellationToken)
    {
        Record(request, parsed, RunModeOutcome.Ask, stage, reason, null);
        return _askUser.ResolveAsync(request, cancellationToken);
    }

    private Task<PermissionDecision> FinishAsync(
        PermissionRequestEvent request,
        RunModeRequest parsed,
        PermissionDecision decision,
        RunModeStage stage,
        string reason,
        CancellationToken cancellationToken,
        PermissionRule? rule = null)
    {
        _ = cancellationToken;
        Record(request, parsed, decision == PermissionDecision.Allow ? RunModeOutcome.Allow : RunModeOutcome.Deny, stage, reason, rule);
        return Task.FromResult(decision);
    }

    private void Record(PermissionRequestEvent request, RunModeRequest parsed, RunModeOutcome outcome, RunModeStage stage, string reason, PermissionRule? rule)
    {
        if (_audit is null)
        {
            return;
        }

        var subject = parsed.Command ?? parsed.McpServer ?? (parsed.Paths is { Count: > 0 } pathList ? pathList[0] : parsed.ToolName);
        _audit.Record(new RunModeAuditEntry(request.CallId, parsed.ToolName, parsed.Kind, subject, new RunModeDecision(outcome, stage, reason, rule), DateTimeOffset.UtcNow));
    }

    private bool ArgumentAllowed(string argument)
    {
        var root = _workingDirectory;
        var path = Path.IsPathRooted(argument) || argument.StartsWith('~') ? argument : Path.Combine(root, argument);
        return _boundary.IsAllowed(path, _trustedFolders, _storageFolder, root);
    }

    private static bool IsDeleting(string kind, RunModeRequest parsed) =>
        string.Equals(kind, "delete", StringComparison.Ordinal)
        || (parsed.Command is { Length: > 0 } command && ShellDeleteDetector.IsDeleting(command));

    private bool WritesAllowed(RunModeRequest parsed, string kind)
    {
        var readWrite = _folderGrants!
            .Where(grant => grant.Access == CoworkFolderAccess.ReadWrite)
            .Select(grant => grant.Path)
            .ToArray();
        var paths = PathsOf(parsed, kind);
        var relativeRoot = parsed.WorkingDirectory is { Length: > 0 } directory ? directory : _workingDirectory;
        foreach (var path in paths)
        {
            if (!_boundary.IsWriteAllowed(path, readWrite, _storageFolder, relativeRoot))
            {
                return false;
            }
        }

        return paths.Count > 0;
    }

    private bool PathsAllowed(RunModeRequest parsed, string kind)
    {
        var paths = PathsOf(parsed, kind);
        if (paths.Count == 0)
        {
            return false;
        }

        var relativeRoot = parsed.WorkingDirectory is { Length: > 0 } directory ? directory : _workingDirectory;
        foreach (var path in paths)
        {
            if (!_boundary.IsAllowed(path, _trustedFolders, _storageFolder, relativeRoot))
            {
                return false;
            }
        }

        return true;
    }

    private List<string> PathsOf(RunModeRequest parsed, string kind)
    {
        var paths = new List<string>();
        if (parsed.Paths is { Count: > 0 })
        {
            paths.AddRange(parsed.Paths);
        }

        if (paths.Count == 0 && string.Equals(kind, "execute", StringComparison.Ordinal))
        {
            paths.Add(parsed.WorkingDirectory is { Length: > 0 } cwd ? cwd : _workingDirectory);
        }

        if (parsed.WorkingDirectory is { Length: > 0 } working && string.Equals(kind, "execute", StringComparison.Ordinal) && !paths.Contains(working, StringComparer.Ordinal))
        {
            paths.Add(working);
        }

        return paths;
    }

    private static PermissionRule? MatchMcp(IReadOnlyList<PermissionRule> rules, string server, string tool)
    {
        foreach (var rule in rules)
        {
            if (rule.Token.Kind == PermissionTokenKind.Mcp && KnownSafeShell.MatchesMcpRule(rule.Token.Argument, server, tool))
            {
                return rule;
            }
        }

        return null;
    }

    private static PermissionRule? MatchShell(IReadOnlyList<PermissionRule> rules, string command, bool forDeny)
    {
        foreach (var rule in rules)
        {
            if (rule.Token.Kind == PermissionTokenKind.Shell && KnownSafeShell.MatchesShellRule(command, rule.Token.Argument, forDeny))
            {
                return rule;
            }
        }

        return null;
    }

    private static bool IsCoworkTool(string? name) =>
        name is not null && name.StartsWith("mcp__cowork_", StringComparison.Ordinal);
}
