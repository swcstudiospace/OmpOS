using System.Globalization;
using System.Text;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Safety;

namespace OmpOS.Native.Composition;

/// <summary>
/// The run mode policy of one workspace root, kept current. It is rebuilt when the application starts a session (<see cref="RefreshAsync"/>), when the user saves the run mode (the
/// <see cref="RunModeHub"/> refreshes every holder) and, for the permission and sandbox files, whenever one of them changed on disk since the last build: <see cref="Current"/> compares the
/// files' write times before it answers, so an edit of <c>permissions.json</c> applies to the next tool call. A file that cannot be read or parsed never throws: the holder keeps the safe
/// default (nothing allowed in advance, the default read boundary) and reports the reason in <see cref="Notices"/>.
/// </summary>
internal sealed class RunModePolicyHolder : IDisposable
{
    private readonly RunModeHub _hub;
    private readonly string _workspaceRoot;
    private readonly Lock _gate = new();
    private RunMode _mode = RunModes.Default;
    private EffectivePermissions _permissions = EffectivePermissions.Empty;
    private EffectiveSandbox _sandbox = EffectiveSandbox.Default;
    private RunModePolicy _policy;
    private IReadOnlyList<string> _notices = [];
    private string _signature = string.Empty;
    private int _disposed;

    /// <summary>Creates a holder. It holds the default policy until <see cref="RefreshAsync"/> runs.</summary>
    /// <param name="hub">The hub that owns the mode store, the audit log and the list of holders.</param>
    /// <param name="workspaceRoot">The absolute workspace root the policy applies to.</param>
    internal RunModePolicyHolder(RunModeHub hub, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _hub = hub;
        _workspaceRoot = workspaceRoot;
        _policy = new RunModePolicy(_mode, _permissions, _sandbox, workspaceRoot, hub.HomeDirectory);
    }

    /// <summary>Gets the policy in force. It is rebuilt first when a permission or sandbox file changed since the last build.</summary>
    public RunModePolicy Current
    {
        get
        {
            lock (_gate)
            {
                var signature = Signature();
                if (!string.Equals(signature, _signature, StringComparison.Ordinal))
                {
                    Rebuild(_mode, signature);
                }

                return _policy;
            }
        }
    }

    /// <summary>Gets the run mode of the last build.</summary>
    public RunMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    /// <summary>Gets the plain messages about files that could not be read or hold an error, for the session's stage notice. Empty when everything loaded.</summary>
    public IReadOnlyList<string> Notices
    {
        get
        {
            lock (_gate)
            {
                return _notices;
            }
        }
    }

    /// <summary>Gets how omp is told to ask for approvals under the run mode and the files of the last build.</summary>
    public RunModeLaunch Launch
    {
        get
        {
            lock (_gate)
            {
                return RunModeLaunchMapper.Map(_mode, RunModeLaunchMapper.ReadsNeedGate(_permissions, _sandbox));
            }
        }
    }

    /// <summary>Gets the <c>bash</c> block omp enforces in every approval mode for the shell deny rules of the last build, or null when no rule needs one.</summary>
    public string? BashOverlayYaml
    {
        get
        {
            lock (_gate)
            {
                return RunModeOverlay.RenderYaml(RunModeOverlay.BashDenyPatterns(_permissions));
            }
        }
    }

    /// <summary>Reads the run mode and the files again and rebuilds the policy.</summary>
    /// <param name="cancellationToken">Cancels the read of the run mode.</param>
    /// <returns>A task that completes when the new policy is in force.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var mode = await _hub.LoadModeAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            Rebuild(mode, Signature());
        }
    }

    /// <summary>Stops the hub from refreshing this holder.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _hub.Remove(this);
        }
    }

    private void Rebuild(RunMode mode, string signature)
    {
        var notices = new List<string>();
        var trust = _hub.IsTrusted(_workspaceRoot);
        var permissions = EffectivePermissions.Empty;
        var sandbox = EffectiveSandbox.Default;
        try
        {
            permissions = PermissionsLoader.Load(_hub.HomeDirectory, _workspaceRoot, trust);
            sandbox = SandboxLoader.Load(_hub.HomeDirectory, _workspaceRoot, null, trust);
            notices.AddRange(permissions.Diagnostics.Concat(sandbox.Diagnostics).Where(static diagnostic => diagnostic.Severity == SafetyDiagnosticSeverity.Error).Select(Describe));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            notices.Add("The permission files could not be read, so no tool call is allowed in advance and the default read boundary applies. " + exception.GetType().Name);
        }

        _mode = mode;
        _permissions = permissions;
        _sandbox = sandbox;
        _notices = notices;
        _signature = signature;
        _policy = new RunModePolicy(mode, permissions, sandbox, _workspaceRoot, _hub.HomeDirectory);
    }

    private static string Describe(SafetyDiagnostic diagnostic) =>
        string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(diagnostic.File)}: {diagnostic.Message}");

    /// <summary>Returns the write times and sizes of the permission and sandbox files, so a change on disk is seen without a watcher.</summary>
    private string Signature()
    {
        var builder = new StringBuilder();
        foreach (var location in SafetyFileLocations.Permissions(_hub.HomeDirectory, _workspaceRoot).Concat(SafetyFileLocations.Sandbox(_hub.HomeDirectory, _workspaceRoot)))
        {
            try
            {
                var info = new FileInfo(location.Path);
                builder.Append(location.Path).Append('|');
                builder.Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0).Append('|');
                builder.Append(info.Exists ? info.Length : 0).Append('\n');
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                builder.Append(location.Path).Append("|unreadable\n");
            }
        }

        return builder.ToString();
    }
}
