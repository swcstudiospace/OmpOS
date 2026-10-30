using System.Diagnostics;

namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Runs the enabled hooks for one lifecycle event in registry order. A hook starts only when its plugin is not
/// quarantined and consent is recorded for its exact command. Both checks happen before any process is spawned.
/// </summary>
/// <remarks>
/// A failing hook does not stop the others. A blocking hook that fails stops the hooks after it, and those are reported
/// as skipped. A blocking hook that exits with code 2 denies the action. That follows the convention in the Cursor hooks
/// documentation, https://cursor.com/docs/hooks, where exit code 2 blocks an action and other failures let it proceed.
/// A blocking hook with no consent does not start, and it also denies the action, so withholding consent cannot turn a
/// guard into a pass. A blocking hook of a quarantined plugin is treated the same way, with reason
/// <see cref="HookDenialReason.PluginQuarantined"/>. A non-blocking hook of a quarantined plugin is skipped. Each denial
/// reports its <see cref="HookDenialReason"/>.
/// </remarks>
public sealed class HookRunner
{
    private const int DenyExitCode = 2;

    private readonly HookRegistry _registry;
    private readonly PluginStateStore _state;
    private readonly IHookProcessRunner _processRunner;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a runner.
    /// </summary>
    /// <param name="registry">The hooks to run.</param>
    /// <param name="state">The consent and quarantine state.</param>
    /// <param name="processRunner">Starts each hook process. Tests pass a fake that counts spawns.</param>
    /// <param name="options">The timeout. The default is 30 seconds.</param>
    public HookRunner(
        HookRegistry registry,
        PluginStateStore state,
        IHookProcessRunner processRunner,
        HookRunnerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(processRunner);
        var effective = options ?? new HookRunnerOptions();
        if (effective.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The hook timeout must be positive.");
        }

        _registry = registry;
        _state = state;
        _processRunner = processRunner;
        _timeout = effective.Timeout;
    }

    /// <summary>
    /// Runs the enabled hooks for <paramref name="trigger"/>, in registry order.
    /// </summary>
    /// <param name="trigger">The lifecycle event.</param>
    /// <param name="cancellationToken">Cancels the run. The running hook's process tree is killed.</param>
    /// <returns>One record per enabled hook for the event.</returns>
    public async Task<HookRunResult> RunAsync(HookTrigger trigger, CancellationToken cancellationToken)
    {
        var executions = new List<HookExecution>();
        HookDenialReason? denial = null;
        var stopped = false;
        foreach (var hook in _registry.List())
        {
            if (hook.Trigger != trigger || !hook.Enabled)
            {
                continue;
            }

            if (stopped)
            {
                executions.Add(NotRun(hook, HookExecutionStatus.Skipped));
                continue;
            }

            if (_state.FindQuarantine(hook.PluginId) is not null)
            {
                if (!hook.Blocking)
                {
                    executions.Add(NotRun(hook, HookExecutionStatus.Skipped));
                    continue;
                }

                // A blocking hook of a quarantined plugin does not start, and it denies the action.
                executions.Add(NotRun(hook, HookExecutionStatus.PluginQuarantined));
                denial = HookDenialReason.PluginQuarantined;
                stopped = true;
                continue;
            }

            if (!_state.IsConsented(hook.CommandHash))
            {
                executions.Add(NotRun(hook, HookExecutionStatus.AwaitingConsent));
                if (hook.Blocking)
                {
                    // A blocking hook without consent is not started. It denies the action rather than letting it proceed.
                    denial = HookDenialReason.ConsentMissing;
                    stopped = true;
                }
                continue;
            }

            var execution = await ExecuteAsync(hook, cancellationToken).ConfigureAwait(false);
            executions.Add(execution);
            if (execution.Status == HookExecutionStatus.Denied)
            {
                denial = HookDenialReason.ExitCodeTwo;
                stopped = true;
            }
            else if (execution.Status != HookExecutionStatus.Succeeded && hook.Blocking)
            {
                stopped = true;
            }
        }

        return new HookRunResult(trigger, denial, executions);
    }

    private async Task<HookExecution> ExecuteAsync(HookRegistration hook, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_timeout);
        var started = Stopwatch.GetTimestamp();

        // Task.Run turns a synchronous throw from the runner into a faulted task, so every runner failure takes one path.
        var running = Task.Run(() => _processRunner.RunAsync(hook.Command, HookOutput.MaxBytes, limit.Token));
        var fault = await TaskFault.AwaitAsync(running).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        if (fault is OperationCanceledException && limit.IsCancellationRequested)
        {
            // The hook outlived its timeout. The runner has killed its process tree.
            return Ran(hook, HookExecutionStatus.TimedOut, started);
        }

        if (fault is not null)
        {
            // The runner failed, so the plugin that owns the hook is quarantined. Other plugins keep running.
            var reason = FaultReason.Describe($"Hook '{hook.Id}'", fault);
            await _state.QuarantineAsync(hook.PluginId, reason, cancellationToken).ConfigureAwait(false);
            return Ran(hook, HookExecutionStatus.Faulted, started);
        }

        var outcome = await running.ConfigureAwait(false);
        var (stdout, stdoutCut) = HookOutput.Bound(outcome.StandardOutput);
        var (stderr, stderrCut) = HookOutput.Bound(outcome.StandardError);
        return Ran(
            hook,
            Classify(hook.Blocking, outcome.ExitCode),
            started,
            outcome.ExitCode,
            stdout,
            stderr,
            outcome.StandardOutputTruncated || stdoutCut,
            outcome.StandardErrorTruncated || stderrCut);
    }

    private static HookExecutionStatus Classify(bool blocking, int exitCode)
    {
        if (exitCode == 0)
        {
            return HookExecutionStatus.Succeeded;
        }

        // Exit code 2 from a blocking hook denies the action. Convention: https://cursor.com/docs/hooks
        return blocking && exitCode == DenyExitCode ? HookExecutionStatus.Denied : HookExecutionStatus.Failed;
    }

    private static HookExecution NotRun(HookRegistration hook, HookExecutionStatus status) =>
        new(hook.Id, hook.PluginId, status, hook.CommandHash, null, TimeSpan.Zero, string.Empty, string.Empty, false, false);

    private static HookExecution Ran(
        HookRegistration hook,
        HookExecutionStatus status,
        long started,
        int? exitCode = null,
        string standardOutput = "",
        string standardError = "",
        bool standardOutputTruncated = false,
        bool standardErrorTruncated = false) =>
        new(
            hook.Id,
            hook.PluginId,
            status,
            hook.CommandHash,
            exitCode,
            Stopwatch.GetElapsedTime(started),
            standardOutput,
            standardError,
            standardOutputTruncated,
            standardErrorTruncated);
}
