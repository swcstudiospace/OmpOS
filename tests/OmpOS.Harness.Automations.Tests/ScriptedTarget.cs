using OmpOS.Harness.Automations.Hosted;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// An <see cref="IAutomationTarget"/> a test controls: it records every request, and each start either completes with a result the test
/// supplies, throws, or waits until the test releases it.
/// </summary>
internal sealed class ScriptedTarget : IAutomationTarget
{
    private readonly Lock _gate = new();
    private readonly List<AutomationRunRequest> _requests = [];
    private readonly List<TaskCompletionSource<AutomationTargetResult>> _gates = [];

    /// <summary>Creates a target of a kind.</summary>
    /// <param name="kind">The kind.</param>
    public ScriptedTarget(AutomationTargetKind kind = AutomationTargetKind.Cloud)
    {
        Kind = kind;
    }

    /// <inheritdoc />
    public AutomationTargetKind Kind { get; }

    /// <summary>Gets or sets what a start returns when it is not held. The default is a launched result.</summary>
    public Func<AutomationRunRequest, AutomationTargetResult> Respond { get; set; } = static request => AutomationTargetResult.Launched("agent-" + request.RunId[..6]);

    /// <summary>Gets or sets an exception every start throws, or null.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Gets or sets whether a start waits for <see cref="Release"/> (or cancellation) before it returns.</summary>
    public bool Hold { get; set; }

    /// <summary>Gets the requests received so far.</summary>
    public IReadOnlyList<AutomationRunRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>Gets how many held starts are waiting.</summary>
    public int Waiting
    {
        get
        {
            lock (_gate)
            {
                return _gates.Count(static gate => !gate.Task.IsCompleted);
            }
        }
    }

    /// <summary>Waits until at least <paramref name="expected"/> starts have been held in total, then releases every held start.</summary>
    /// <param name="expected">The number of held starts, counting ones already released, to wait for.</param>
    /// <param name="cancellationToken">Cancels the wait, which fails the test.</param>
    /// <param name="result">The result, or null for a succeeded one.</param>
    /// <returns>A task that completes when the starts are released.</returns>
    public async Task ReleaseWhenHeldAsync(int expected, CancellationToken cancellationToken, AutomationTargetResult? result = null)
    {
        await ScriptedTargetWait.UntilAsync(
            () =>
            {
                lock (_gate)
                {
                    return _gates.Count >= expected;
                }
            },
            cancellationToken).ConfigureAwait(false);
        Release(result);
    }

    /// <summary>Releases every held start with a result.</summary>
    /// <param name="result">The result, or null for a succeeded one.</param>
    public void Release(AutomationTargetResult? result = null)
    {
        lock (_gate)
        {
            foreach (var gate in _gates)
            {
                _ = gate.TrySetResult(result ?? AutomationTargetResult.Succeeded());
            }
        }
    }

    /// <inheritdoc />
    public async Task<AutomationTargetResult> RunAsync(AutomationRunRequest request, CancellationToken cancellationToken)
    {
        TaskCompletionSource<AutomationTargetResult>? gate = null;
        lock (_gate)
        {
            _requests.Add(request);
            if (Hold)
            {
                gate = new TaskCompletionSource<AutomationTargetResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _gates.Add(gate);
            }
        }

        if (Throw is not null)
        {
            throw Throw;
        }

        if (gate is null)
        {
            return Respond(request);
        }

        using var registration = cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken));
        return await gate.Task.ConfigureAwait(false);
    }
}
