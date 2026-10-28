using OmpOS.Harness.Abstractions;
using OmpOS.Presentation.Worktrees;

namespace OmpOS.Native.Agents;

/// <summary>
/// Runs one best-of-n candidate: a session of its own in the candidate's worktree that is given the task and answers with its last message. The candidates run unattended, so there is
/// nobody to ask for approval: the command is available only in the Run Everything mode, which is the user's own choice to let agents act without asking. The worktree keeps the main checkout
/// untouched until the user applies a candidate.
/// </summary>
internal sealed class BestOfNCandidateRunner
{
    private readonly SessionEngineSet _owner;

    /// <summary>Creates the runner over the head's engine set.</summary>
    /// <param name="owner">The engine set that composes the candidate engines.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public BestOfNCandidateRunner(SessionEngineSet owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Runs the candidate and returns the text of its last answer.</summary>
    /// <param name="request">The task, the candidate's index and its worktree.</param>
    /// <param name="cancellationToken">Cancels the candidate.</param>
    /// <returns>The last assistant text.</returns>
    /// <exception cref="InvalidOperationException">The run mode is not Run Everything, or the candidate failed or answered nothing.</exception>
    public async Task<string> RunAsync(BestOfNRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await _owner.IsRunEverythingAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Best of n runs its candidates without asking you, so it needs the Run Everything mode. Choose it on the Run mode page and try again.");
        }

        var engine = _owner.ComposeUnattendedEngine(request.Run.Path);
        await using (engine.Host.ConfigureAwait(false))
        {
            return await EngineAnswer.CollectAsync(engine.Host.SubmitPromptAsync(request.Prompt, new PromptOptions(), cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }
}
