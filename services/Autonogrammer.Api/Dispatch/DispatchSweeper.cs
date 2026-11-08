using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Moves overdue dispatch commands on (ADR-0036). A queued or delivered command past its expiry becomes expired. An awaiting-approval command past its
/// approval timeout fails approval_timeout. A received, accepted or running command past its run limit fails timeout. The store returns only overdue
/// commands, so a command that is not due never takes a slot from one that is. Each move is one transaction with its feed event.
/// The store is resolved per pass, so the host starts without one and a disabled dispatch never needs it.
/// </summary>
internal sealed class DispatchSweeper(IServiceScopeFactory scopes, TimeProvider time, IOptions<DispatchOptions> options, ILogger<DispatchSweeper> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private const int MaxPagesPerPass = 20;
    private const string FeedKind = "command.state";
    private static readonly TimeSpan Cadence = TimeSpan.FromSeconds(15);
    private static readonly Action<ILogger, Exception?> PassFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(9401, nameof(PassFailed)),
        "The dispatch sweeper pass failed; the next pass runs in 15 seconds");

    /// <summary>Runs one pass and returns how many commands it moved. The pass reads pages at one set of cutoffs and reads the next page while the last one was full, up to the page cap.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>The number of commands the pass moved.</returns>
    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var store = scope.ServiceProvider.GetService<IDispatchStore>();
        if (store is null)
        {
            return 0;
        }

        var settings = options.Value;
        var now = time.GetUtcNow();
        var cutoffs = new DispatchSweepCutoffs(now, now - settings.CommandApprovalTimeout, now - settings.RunLimits.Session, now - settings.RunLimits.Task);
        var moved = 0;
        for (var page = 0; page < MaxPagesPerPass; page++)
        {
            var overdue = await store.ListOverdueAsync(cutoffs, BatchSize, cancellationToken).ConfigureAwait(false);
            foreach (var candidate in overdue)
            {
                if (Move(candidate) is not { } move)
                {
                    continue;
                }

                var feed = new DispatchFeedDraft(FeedKind, Payload(candidate.CommandId, move));
                var transition = await store.TryTransitionAsync(candidate.AccountId, candidate.CommandId, candidate.State, move.Desired, move.Reason, feed, cancellationToken).ConfigureAwait(false);
                if (transition.Status == DispatchTransitionStatus.Applied)
                {
                    moved++;
                }
            }

            if (overdue.Count < BatchSize)
            {
                break;
            }
        }

        return moved;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !options.Value.Sweeper.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(Cadence, time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                _ = await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
            {
                PassFailed(logger, exception);
            }
        }
    }

    private static (DispatchCommandState Desired, DispatchFailureReason Reason)? Move(DispatchSweepCandidate candidate) => candidate.State switch
    {
        DispatchCommandState.Queued or DispatchCommandState.Delivered => (DispatchCommandState.Expired, DispatchFailureReason.Expired),
        DispatchCommandState.AwaitingApproval => (DispatchCommandState.Failed, DispatchFailureReason.ApprovalTimeout),
        DispatchCommandState.Received or DispatchCommandState.Accepted or DispatchCommandState.Running => (DispatchCommandState.Failed, DispatchFailureReason.Timeout),
        _ => null,
    };

    private static string Payload(string commandId, (DispatchCommandState Desired, DispatchFailureReason Reason) move) => new JsonObject
    {
        ["commandId"] = commandId,
        ["state"] = DispatchCommandStateWire.ToWire(move.Desired),
        ["reason"] = DispatchFailureReasonWire.ToWire(move.Reason),
    }.ToJsonString();
}
