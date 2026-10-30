using Autonogrammer.Workspace;
using Autonogrammer.Worktrees;

namespace Autonogrammer.Native.Composition.Checkpoints;

/// <summary>
/// Answers the consent a worktree setup needs from the setup consent store and prompt the head already has: a stored approval of the exact setup answers at once,
/// otherwise the user is asked and only a yes is recorded. A prompt that fails or is cancelled is a refusal.
/// </summary>
internal sealed class WorktreeSetupConsentAdapter : IWorktreeSetupConsent
{
    private const string RootPathVariable = "ROOT_WORKTREE_PATH";

    private readonly ISetupConsentStore _store;
    private readonly ISetupConsentPrompt _prompt;

    /// <summary>Initializes a new instance of the <see cref="WorktreeSetupConsentAdapter"/> class.</summary>
    /// <param name="store">Where approvals are kept.</param>
    /// <param name="prompt">Asks the user.</param>
    public WorktreeSetupConsentAdapter(ISetupConsentStore store, ISetupConsentPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(prompt);
        _store = store;
        _prompt = prompt;
    }

    /// <inheritdoc />
    public async Task<bool> IsGrantedAsync(WorktreeSetupConsentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = new SetupConsentKey(request.Identity.StoreRepositoryPath, request.Identity.Sha256);
        if (await _store.IsGrantedAsync(key, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        SetupConsentDecision decision;
        try
        {
            decision = await _prompt.AskAsync(new SetupConsentRequest(key, request.ConfigPath, Describe(request)), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }

        if (!decision.IsGranted)
        {
            return false;
        }

        await _store.RecordGrantAsync(key, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static WorkspaceSetup Describe(WorktreeSetupConsentRequest request)
    {
        var setup = request.Setup;
        List<SetupStep> steps = setup.IsScript
            ? [new SetupStep("sh", [setup.ScriptPath!], ".")]
            : [.. setup.Commands.Select(static command => new SetupStep("sh", ["-c", command], "."))];
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RootPathVariable] = request.Identity.RepositoryPath,
        };
        return new WorkspaceSetup(steps, environment);
    }
}
