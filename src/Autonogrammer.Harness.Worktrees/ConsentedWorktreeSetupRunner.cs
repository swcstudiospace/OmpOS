namespace Autonogrammer.Worktrees;

/// <summary>
/// The <see cref="IWorktreeSetupRunner"/> that never runs a command without consent. It reads <c>.cursor/worktrees.json</c> the way Cursor does (the worktree first, then the project root), picks
/// the entry for the operating system, works out the <see cref="WorktreeSetupConsentIdentity"/>, asks <see cref="IWorktreeSetupConsent"/> and only then hands the setup to an <see cref="IWorktreeSetupExecutor"/>.
/// </summary>
public sealed class ConsentedWorktreeSetupRunner : IWorktreeSetupRunner
{
    private readonly IWorktreeSetupConsent _consent;
    private readonly IWorktreeSetupExecutor _executor;
    private readonly bool _windows;

    /// <summary>Creates the runner.</summary>
    /// <param name="consent">Decides whether the setup may run.</param>
    /// <param name="executor">Runs it.</param>
    /// <param name="windows">Whether the setup for Windows is the one to run; the operating system when <see langword="null"/>.</param>
    public ConsentedWorktreeSetupRunner(IWorktreeSetupConsent consent, IWorktreeSetupExecutor executor, bool? windows = null)
    {
        ArgumentNullException.ThrowIfNull(consent);
        ArgumentNullException.ThrowIfNull(executor);
        _consent = consent;
        _executor = executor;
        _windows = windows ?? OperatingSystem.IsWindows();
    }

    /// <inheritdoc />
    public async Task<WorktreeSetupResult> RunAsync(WorktreeSetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        CursorWorktreesConfig? config;
        WorktreeSetupConsentIdentity identity;
        CursorWorktreeSetup? setup;
        try
        {
            config = CursorWorktreesConfig.Find(request.WorktreePath, request.ProjectRoot);
            setup = config?.SetupFor(_windows);
            if (config is null || setup is null)
            {
                return new WorktreeSetupResult(WorktreeSetupStatus.NoSetup, 0, "This repository has no worktree setup for this system.");
            }

            identity = WorktreeSetupConsentIdentity.Create(request.ProjectRoot, config, setup);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new WorktreeSetupResult(WorktreeSetupStatus.Invalid, 0, "The worktree setup could not be read, so nothing ran: " + exception.Message);
        }

        var granted = await _consent.IsGrantedAsync(new WorktreeSetupConsentRequest(identity, config.FilePath, setup), cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            return new WorktreeSetupResult(WorktreeSetupStatus.ConsentDenied, 0, "The worktree setup was not approved, so nothing ran.");
        }

        return await _executor.ExecuteAsync(new WorktreeSetupExecution(request.WorktreePath, request.ProjectRoot, setup), cancellationToken).ConfigureAwait(false);
    }
}
