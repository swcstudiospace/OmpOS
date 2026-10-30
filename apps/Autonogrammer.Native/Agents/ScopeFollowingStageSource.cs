using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Wraps a stage source that depends on the omp session identifier or the Graph identifier of its scope. It observes with the scope
/// from <see cref="SessionScopeProvider"/> and, whenever that scope changes, stops the inner source and starts it again with the new
/// one. The inner source then reads the files of the session as they are now; a repeated event is dropped by the composite above,
/// which keeps one signature per stage.
/// </summary>
internal sealed class ScopeFollowingStageSource : IStageSource
{
    private readonly IStageSource _inner;
    private readonly SessionScopeProvider _scope;

    /// <summary>Creates the wrapper.</summary>
    /// <param name="inner">The source to observe with the current scope.</param>
    /// <param name="scope">The live scope of the session the source observes.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public ScopeFollowingStageSource(IStageSource inner, SessionScopeProvider scope)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(scope);
        _inner = inner;
        _scope = scope;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        while (!cancellationToken.IsCancellationRequested)
        {
            var current = _scope.Current;
            using var generation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var enumerator = _inner.ObserveAsync(current, generation.Token).GetAsyncEnumerator(generation.Token);
            var changed = _scope.WhenChangedAfterAsync(current);
            Task<bool>? move = null;
            var restart = false;
            try
            {
                move = enumerator.MoveNextAsync().AsTask();
                while (true)
                {
                    var first = await Task.WhenAny(move, changed).ConfigureAwait(false);
                    if (!ReferenceEquals(first, move))
                    {
                        restart = true;
                        break;
                    }

                    if (!await SettleAsync(move).ConfigureAwait(false))
                    {
                        break;
                    }

                    var next = enumerator.Current;
                    move = enumerator.MoveNextAsync().AsTask();
                    yield return next;
                }
            }
            finally
            {
                // The inner enumerator may be disposed only once its last move has ended.
                await generation.CancelAsync().ConfigureAwait(false);
                if (move is not null)
                {
                    await DrainAsync(move).ConfigureAwait(false);
                }

                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (!restart)
            {
                yield break;
            }
        }
    }

    /// <summary>Awaits a move that may have been cancelled. Cancellation, from either token, is a normal end of the sequence.</summary>
    private static async Task<bool> SettleAsync(Task<bool> move)
    {
        try
        {
            return await move.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Waits for a move to end and discards its outcome, so a fault of a source being stopped never hides the reason it was stopped.</summary>
    private static async Task DrainAsync(Task<bool> move)
    {
        try
        {
            await move.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The source is being stopped; its last move may end in cancellation or a fault, and neither matters now.
        }
    }
}
