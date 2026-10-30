using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Safety;

namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// The permission seam for a Cowork omp child. It denies a path outside the trusted folders and the Storage folder,
/// always asks before a delete, allows <c>mcp__cowork_*</c> at the omp level, and otherwise follows the mode.
/// A request it cannot settle is passed to the resolver that asks the user. Decisions are not logged.
/// </summary>
public sealed class CoworkPermissionResolver : IPermissionResolver
{
    private static readonly HashSet<string> TaintedActuatingKinds = new(StringComparer.Ordinal) { "edit", "delete", "move", "execute" };
    private static readonly HashSet<string> PathKinds = new(StringComparer.Ordinal) { "read", "edit", "delete", "move", "search", "execute" };
    private static readonly HashSet<string> AlwaysAllowKinds = new(StringComparer.Ordinal) { "think", "switch_mode" };

    private readonly IPermissionResolver _askUser;
    private readonly CoworkPathBoundary _boundary;
    private readonly IReadOnlyList<string> _trustedFolders;
    private readonly string? _storageFolder;
    private readonly string _workingDirectory;
    private readonly Tools.CoworkPermissionMode _effectiveMode;
    private readonly Func<bool> _isTainted;
    private readonly HashSet<string> _catalogServers;

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
        IReadOnlyCollection<string>? catalogServers = null)
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
        _effectiveMode = CoworkPermissionMode.Effective(mode, filesystemIsolated, skipAcknowledged);
        _isTainted = isTainted;
        _catalogServers = new HashSet<string>(catalogServers ?? [], StringComparer.Ordinal);
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
            return Task.FromResult(PermissionDecision.Allow);
        }

        if (AlwaysAllowKinds.Contains(kind))
        {
            return Task.FromResult(PermissionDecision.Allow);
        }

        if (parsed.Kind == RunModeToolKind.Mcp)
        {
            return DecideMcpAsync(request, parsed, cancellationToken);
        }

        if (PathKinds.Contains(kind))
        {
            if (!PathsAllowed(parsed, kind))
            {
                return Task.FromResult(PermissionDecision.Deny);
            }

            if (string.Equals(kind, "delete", StringComparison.Ordinal) || (Tainted && TaintedActuatingKinds.Contains(kind)))
            {
                return AskAsync(request, cancellationToken);
            }

            return FollowModeAsync(request, cancellationToken);
        }

        return FollowModeAsync(request, cancellationToken);
    }

    private bool Tainted => _isTainted();

    private Task<PermissionDecision> DecideMcpAsync(PermissionRequestEvent request, RunModeRequest parsed, CancellationToken cancellationToken)
    {
        var server = parsed.McpServer ?? string.Empty;
        if (Tainted && _catalogServers.Contains(server))
        {
            return AskAsync(request, cancellationToken);
        }

        if (_effectiveMode == Tools.CoworkPermissionMode.Skip && _catalogServers.Contains(server))
        {
            return Task.FromResult(PermissionDecision.Allow);
        }

        return AskAsync(request, cancellationToken);
    }

    private Task<PermissionDecision> FollowModeAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        if (_effectiveMode == Tools.CoworkPermissionMode.Skip)
        {
            return Task.FromResult(PermissionDecision.Allow);
        }

        return AskAsync(request, cancellationToken);
    }

    private Task<PermissionDecision> AskAsync(PermissionRequestEvent request, CancellationToken cancellationToken) =>
        _askUser.ResolveAsync(request, cancellationToken);

    private bool PathsAllowed(RunModeRequest parsed, string kind)
    {
        var paths = new List<string>();
        if (parsed.Paths is { Count: > 0 })
        {
            paths.AddRange(parsed.Paths);
        }

        if (paths.Count == 0)
        {
            if (string.Equals(kind, "execute", StringComparison.Ordinal) && parsed.WorkingDirectory is { Length: > 0 } cwd)
            {
                paths.Add(cwd);
            }
            else if (string.Equals(kind, "execute", StringComparison.Ordinal))
            {
                paths.Add(_workingDirectory);
            }
            else
            {
                return false;
            }
        }

        if (parsed.WorkingDirectory is { Length: > 0 } working && string.Equals(kind, "execute", StringComparison.Ordinal) && !paths.Contains(working, StringComparer.Ordinal))
        {
            paths.Add(working);
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

    private static bool IsCoworkTool(string? name) =>
        name is not null && name.StartsWith("mcp__cowork_", StringComparison.Ordinal);
}
