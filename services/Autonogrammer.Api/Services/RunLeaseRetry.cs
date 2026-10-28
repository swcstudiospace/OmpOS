using System.Diagnostics;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Retries a run-lease acquisition for a bounded time before a turn is refused. The first attempt is immediate; each refusal waits
/// <see cref="Step"/> before the next attempt, and no attempt starts after <see cref="Deadline"/> has elapsed.
/// </summary>
/// <remarks>
/// A run releases its lease a few milliseconds after its terminal event reaches the client, so a client that submits its next prompt
/// as soon as it sees that event can find the lease still held by a run that has already settled. The retry absorbs that window. A lease
/// that another owner holds for longer than <see cref="Deadline"/> is still refused.
/// </remarks>
internal static class RunLeaseRetry
{
    /// <summary>Gets the pause between two acquisition attempts.</summary>
    internal static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);

    /// <summary>Gets the total time within which acquisition is attempted before the turn is refused.</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    /// <summary>Attempts the acquisition until it succeeds or the deadline passes.</summary>
    /// <param name="attempt">Makes one acquisition attempt; returns the lease, or null when another owner holds it.</param>
    /// <param name="cancellationToken">Checked before every attempt and during every pause.</param>
    /// <returns>The acquired lease, or null when the deadline passed with the lease still held.</returns>
    public static async Task<ISessionRunLease?> AcquireWithinDeadlineAsync(
        Func<CancellationToken, Task<ISessionRunLease?>> attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lease = await attempt(cancellationToken).ConfigureAwait(false);
            if (lease is not null)
            {
                return lease;
            }

            var remaining = Deadline - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            await Task.Delay(remaining < Step ? remaining : Step, cancellationToken).ConfigureAwait(false);
        }
    }
}
