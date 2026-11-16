using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>Moves a stored command along a path of compare-and-set transitions, and waits for a command to reach a state.</summary>
internal static class CommandStates
{
    /// <summary>The path a command takes from queued to running.</summary>
    internal static readonly (DispatchCommandState From, DispatchCommandState To)[] ToRunning =
    [
        (DispatchCommandState.Queued, DispatchCommandState.Delivered),
        (DispatchCommandState.Delivered, DispatchCommandState.Received),
        (DispatchCommandState.Received, DispatchCommandState.Accepted),
        (DispatchCommandState.Accepted, DispatchCommandState.Running),
    ];

    /// <summary>Applies each transition in order, and fails the test when one is not applied.</summary>
    /// <param name="store">The store.</param>
    /// <param name="accountId">The account that owns the command.</param>
    /// <param name="commandId">The command.</param>
    /// <param name="path">The transitions, in order.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when every transition is applied.</returns>
    internal static async Task AdvanceAsync(IDispatchStore store, string accountId, string commandId, IEnumerable<(DispatchCommandState From, DispatchCommandState To)> path, CancellationToken cancellationToken)
    {
        foreach (var (from, to) in path)
        {
            var result = await store.TryTransitionAsync(accountId, commandId, from, to, null, null, cancellationToken).ConfigureAwait(true);
            if (result.Status != DispatchTransitionStatus.Applied)
            {
                throw new InvalidOperationException("Transition " + from + " to " + to + " was not applied: " + result.Status);
            }
        }
    }

    /// <summary>Polls a command until it reports the expected state, or the budget ends.</summary>
    /// <param name="store">The store.</param>
    /// <param name="accountId">The account that owns the command.</param>
    /// <param name="commandId">The command.</param>
    /// <param name="expected">The state to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>True when the command reached the state in time.</returns>
    internal static async Task<bool> WaitForAsync(IDispatchStore store, string accountId, string commandId, DispatchCommandState expected, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var row = await store.GetCommandAsync(accountId, commandId, cancellationToken).ConfigureAwait(true);
            if (row?.State == expected)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(true);
        }

        return false;
    }
}
