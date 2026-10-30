using Autonogrammer.Presentation.Settings.Worktrees;
using Autonogrammer.Workspace;
using Autonogrammer.Worktrees;

namespace Autonogrammer.Native.Settings;

/// <summary>
/// The head's source of the Worktrees page. It reads <c>.cursor/worktrees.json</c> of the workspace root with <see cref="CursorWorktreesConfig"/>, never runs
/// any of it, and asks the setup consent store whether this exact version of the setup (file and script) was approved, using the same identity and key the worktree setup runner uses.
/// </summary>
internal sealed class WorktreesSettingsSource : IWorktreesSettingsSource
{
    private readonly string _workspaceRoot;
    private readonly ISetupConsentStore _consent;

    /// <summary>Initializes a new instance of the <see cref="WorktreesSettingsSource"/> class.</summary>
    /// <param name="workspaceRoot">The workspace root, which is also the project root of its worktrees.</param>
    /// <param name="consent">Where the approvals of setup files are kept.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="consent"/> is null.</exception>
    public WorktreesSettingsSource(string workspaceRoot, ISetupConsentStore consent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(consent);
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _consent = consent;
    }

    /// <inheritdoc/>
    public async Task<WorktreesSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        CursorWorktreesConfig? config;
        CursorWorktreeSetup? setup;
        WorktreeSetupConsentIdentity? identity = null;
        try
        {
            config = CursorWorktreesConfig.Find(_workspaceRoot, _workspaceRoot);
            setup = config?.SetupFor(OperatingSystem.IsWindows());
            if (config is not null && setup is not null)
            {
                identity = WorktreeSetupConsentIdentity.Create(_workspaceRoot, config, setup);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return Snapshot(WorktreeSetupState.Invalid, string.Empty, [], string.Empty, exception.Message);
        }

        if (config is null)
        {
            return Snapshot(WorktreeSetupState.NoSetupFile, string.Empty, [], string.Empty, string.Empty);
        }

        if (setup is null || identity is null)
        {
            return Snapshot(WorktreeSetupState.NoSetupForThisSystem, config.FilePath, [], string.Empty, string.Empty);
        }

        var granted = await _consent
            .IsGrantedAsync(new SetupConsentKey(identity.StoreRepositoryPath, identity.Sha256), cancellationToken)
            .ConfigureAwait(false);
        return Snapshot(
            granted ? WorktreeSetupState.Granted : WorktreeSetupState.NotGranted,
            config.FilePath,
            setup.Commands,
            setup.ScriptPath ?? string.Empty,
            string.Empty);
    }

    private WorktreesSnapshot Snapshot(WorktreeSetupState state, string file, IReadOnlyList<string> commands, string script, string error) =>
        new(_workspaceRoot, WorktreeRunSet.DefaultMaxWorktrees, state, file, commands, script, error);
}
