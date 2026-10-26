using OmpOS.Contracts.Agents;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Native.Agents;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>Shared helpers of the agent-loop tests: head options over a temporary tree, a session with a slot, a turn sent through a slot, and bounded waits.</summary>
internal static class AgentLoopFixtures
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    /// <summary>Builds head options over a tree and a scripted omp fleet. The maintenance timer is off unless a test asks for it.</summary>
    public static AgentHeadOptions Options(
        TempTree tree,
        ScriptedOmp? omp,
        Func<string, string?>? variables = null,
        HttpMessageHandler? substrate = null,
        TimeProvider? clock = null,
        TimeSpan? maintenance = null,
        string? ompPath = null,
        TimeSpan? detailsRefresh = null,
        TimeSpan? steerAck = null,
        OmpLocatorOptions? terminalLocator = null) => new()
        {
            PluginsRoot = tree.Plugins,
            StateRoot = tree.State,
            WorkspaceRoot = tree.Workspace,
            TransportFactory = omp?.TransportFactory,
            GetVariable = variables,
            SubstrateHandler = substrate,
            TimeProvider = clock,
            SelectMostRecentSession = false,
            MaintenanceInterval = maintenance ?? Timeout.InfiniteTimeSpan,
            OmpPath = ompPath,
            DetailsRefreshInterval = detailsRefresh ?? Timeout.InfiniteTimeSpan,
            SteerAckTimeout = steerAck,
            TerminalLocator = terminalLocator,
        };

    /// <summary>Creates a session in the registry and its slot, as the panel does when the user starts an agent.</summary>
    public static async Task<(string Id, SessionSlot Slot)> NewSlotAsync(AgentHead head, CancellationToken cancellationToken)
    {
        var id = await head.Host.CreateSessionAsync(cancellationToken).ConfigureAwait(true);
        return (id, head.Host.CreateSlot(id, null, null));
    }

    /// <summary>Sends one prompt through a slot's send path and returns the events of the turn.</summary>
    public static async Task<List<HarnessEvent>> SendAsync(SessionSlot slot, string text, CancellationToken cancellationToken)
    {
        var events = new List<HarnessEvent>();
        await foreach (var harnessEvent in slot.Prompts.SubmitAsync(text, cancellationToken).ConfigureAwait(true))
        {
            events.Add(harnessEvent);
        }

        return events;
    }

    /// <summary>Observes a slot's stage sources until <paramref name="done"/> accepts what was seen, or the wait limit passes, and returns everything seen.</summary>
    public static async Task<List<StageEvent>> ObserveUntilAsync(SessionSlot slot, Func<IReadOnlyList<StageEvent>, bool> done, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(WaitLimit);
        var seen = new List<StageEvent>();
        try
        {
            await foreach (var stageEvent in slot.Stages.ObserveAsync(slot.Scope.Current, limit.Token).WithCancellation(limit.Token).ConfigureAwait(true))
            {
                seen.Add(stageEvent);
                if (done(seen))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The wait limit passed: the caller's assertions describe what was missing.
        }

        return seen;
    }

    /// <summary>Polls a condition until it holds, up to the wait limit, with short pauses that only give other threads their turn.</summary>
    public static async Task EventuallyAsync(Func<bool> condition, string description, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {description}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(15), cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Returns whether a process is still running. A zombie that has exited is not running.</summary>
    public static bool IsProcessRunning(int processId)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{processId}/stat");
            var state = stat[(stat.LastIndexOf(')') + 2)..][0];
            return state is not ('Z' or 'X');
        }
        catch (IOException)
        {
            return false;
        }
    }
}
