using Autonogrammer.Presentation.Worktrees;

namespace Autonogrammer.Native.Composition.Checkpoints;

/// <summary>
/// Runs one best-of-n candidate through a delegate the head provides (it opens an agent session in the candidate's worktree, sends the prompt and returns the last assistant text).
/// A fault of the candidate is reported as a failed candidate, never thrown; only a cancellation by the caller propagates.
/// </summary>
internal sealed class BestOfNHost : IBestOfNHost
{
    private readonly Func<BestOfNRunRequest, CancellationToken, Task<string>> _runCandidate;

    /// <summary>Initializes a new instance of the <see cref="BestOfNHost"/> class.</summary>
    /// <param name="runCandidate">Runs a candidate and returns its final answer.</param>
    public BestOfNHost(Func<BestOfNRunRequest, CancellationToken, Task<string>> runCandidate)
    {
        ArgumentNullException.ThrowIfNull(runCandidate);
        _runCandidate = runCandidate;
    }

    /// <inheritdoc />
    public async Task<BestOfNRunResult> RunAsync(BestOfNRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var text = await _runCandidate(request, cancellationToken).ConfigureAwait(false);
            return new BestOfNRunResult(true, text ?? string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new BestOfNRunResult(false, string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message);
        }
    }
}
