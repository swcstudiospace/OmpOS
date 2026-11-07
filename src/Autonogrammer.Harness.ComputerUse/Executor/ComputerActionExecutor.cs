using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse.Executor;

/// <summary>
/// The only type that injects. It holds the single backend reference used for injection, consumes the single-use ticket,
/// and serialises actions so nothing is injected while an approval prompt is open.
/// 73-03 appends Tools, Frames and Audit composition only through a separate ComputerUseToolsComposition file.
/// </summary>
public sealed class ComputerActionExecutor : IDisposable
{
    /// <summary>Characters per type run.</summary>
    public const int TypeRunLength = 16;

    private readonly IComputerUseBackend _backend;
    private readonly ComputerActionGate _gate;
    private readonly KillSwitchCoordinator _killSwitch;
    private readonly TimeProvider _timeProvider;
    private readonly int _takeoverPx;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private int _queued;

    /// <summary>Actions that have entered the process-wide lock, including the holder.</summary>
    public int Queued => _queued;
    private readonly Lock _primitives = new();
    private int _primitiveStamp;
    private PointerState? _leftPointer;

    /// <summary>Creates the executor. This is the only type that calls <see cref="IComputerUseBackend.InjectAsync"/>.</summary>
    /// <param name="backend">The backend.</param>
    /// <param name="gate">The gate.</param>
    /// <param name="killSwitch">Revocation and takeover latch.</param>
    /// <param name="time">Clock for the ticket lifetime.</param>
    /// <param name="takeoverPx">Pointer displacement that pauses. Default 24.</param>
    public ComputerActionExecutor(
        IComputerUseBackend backend,
        ComputerActionGate gate,
        KillSwitchCoordinator killSwitch,
        TimeProvider time,
        int takeoverPx = 24)
    {
        _backend = backend;
        _gate = gate;
        _killSwitch = killSwitch;
        _timeProvider = time;
        _takeoverPx = takeoverPx;
    }

    /// <summary>Configured takeover threshold in pixels.</summary>
    public int TakeoverPx => _takeoverPx;

    /// <summary>
    /// Authorises and injects. The process-wide lock is held across the prompt, so a second action cannot inject while it is open.
    /// </summary>
    /// <param name="request">Host-built request.</param>
    /// <param name="hostAction">Host-built facts.</param>
    /// <param name="primitives">Primitives. Type text is split into runs of 16.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The result.</returns>
    public async ValueTask<ComputerExecutionResult> ExecuteAsync(
        ComputerActionRequest request,
        ComputerHostAction hostAction,
        IReadOnlyList<InputPrimitive> primitives,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(hostAction);
        ArgumentNullException.ThrowIfNull(primitives);
        Interlocked.Increment(ref _queued);
        await _serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _gate.BindHostAction(request.ActionHash, hostAction);
            ComputerGateDecision decision = await _gate.AuthorizeAsync(request, cancellationToken).ConfigureAwait(false);
            if (decision is not ComputerGateAllowed allowed)
            {
                return new ComputerExecutionResult(decision is ComputerGateDenied denied ? denied.Code : DenialCode.InternalError);
            }

            if (!allowed.Ticket.Matches(request.ActionHash)
                || !allowed.Ticket.IsWithinLifetime(_timeProvider.GetUtcNow())
                || !allowed.Ticket.TryConsume())
            {
                return new ComputerExecutionResult(DenialCode.InternalError);
            }

            if (IsKeyboard(hostAction) && await ForegroundIsOwnAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ComputerExecutionResult(DenialCode.OwnWindow);
            }

            ComputerExecutionResult? injected = await InjectSequenceAsync(request, primitives, cancellationToken).ConfigureAwait(false);
            return injected ?? new ComputerExecutionResult(null);
        }
        finally
        {
            _serial.Release();
        }
    }

    /// <summary>Releases the process-wide lock.</summary>
    public void Dispose() => _serial.Dispose();

    private async ValueTask<ComputerExecutionResult?> InjectSequenceAsync(
        ComputerActionRequest request,
        IReadOnlyList<InputPrimitive> primitives,
        CancellationToken cancellationToken)
    {
        string? expectedWindow = request.Target?.WindowId;
        foreach (InputPrimitive primitive in Expand(primitives))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_killSwitch.RevocationToken.IsCancellationRequested)
            {
                return new ComputerExecutionResult(DenialCode.Revoked);
            }

            if (IsPointer(primitive) && await TookOverAsync(cancellationToken).ConfigureAwait(false))
            {
                _killSwitch.NoteTakeover();
                return new ComputerExecutionResult(DenialCode.UserTookOver);
            }

            lock (_primitives)
            {
                _primitiveStamp++;
            }

            await _backend.InjectAsync(primitive, cancellationToken).ConfigureAwait(false);
            if (primitive is PointerMove move)
            {
                _leftPointer = new PointerState { X = move.X, Y = move.Y, Buttons = 0 };
                ComputerExecutionResult? aborted = await ReResolveAsync(move, expectedWindow, cancellationToken).ConfigureAwait(false);
                if (aborted is not null)
                {
                    return aborted;
                }
            }

            if (primitive is TypeText)
            {
                ComputerExecutionResult? changed = await ForegroundChangedAsync(request, cancellationToken).ConfigureAwait(false);
                if (changed is not null)
                {
                    return changed;
                }
            }
        }

        return null;
    }

    private async ValueTask<bool> TookOverAsync(CancellationToken cancellationToken)
    {
        if (_leftPointer is null)
        {
            return false;
        }

        PointerState actual = await _backend.GetPointerAsync(cancellationToken).ConfigureAwait(false);
        double dx = actual.X - _leftPointer.X;
        double dy = actual.Y - _leftPointer.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) > _takeoverPx;
    }

    private async ValueTask<ComputerExecutionResult?> ReResolveAsync(PointerMove move, string? expectedWindow, CancellationToken cancellationToken)
    {
        IReadOnlyList<WindowInfo> windows = await _backend.ListWindowsAsync(cancellationToken).ConfigureAwait(false);
        WindowInfo? hit = null;
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            PixelRect bounds = windows[i].Bounds;
            if (move.X >= bounds.X && move.Y >= bounds.Y && move.X < bounds.X + bounds.Width && move.Y < bounds.Y + bounds.Height)
            {
                hit = windows[i];
                break;
            }
        }

        if (hit is null)
        {
            return null;
        }

        if (hit.IsOwnProcess || (expectedWindow is not null && !string.Equals(hit.Id, expectedWindow, StringComparison.Ordinal)))
        {
            return new ComputerExecutionResult(DenialCode.OwnWindow);
        }

        return null;
    }

    private async ValueTask<bool> ForegroundIsOwnAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<WindowInfo> windows = await _backend.ListWindowsAsync(cancellationToken).ConfigureAwait(false);
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].Focused && windows[i].IsOwnProcess)
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<ComputerExecutionResult?> ForegroundChangedAsync(ComputerActionRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<WindowInfo> windows = await _backend.ListWindowsAsync(cancellationToken).ConfigureAwait(false);
        for (int i = 0; i < windows.Count; i++)
        {
            if (!windows[i].Focused)
            {
                continue;
            }

            if (windows[i].IsOwnProcess)
            {
                return new ComputerExecutionResult(DenialCode.OwnWindow);
            }

            if (request.Target?.AppIdentity is string identity
                && !string.Equals(windows[i].AppIdentity, identity, StringComparison.OrdinalIgnoreCase))
            {
                return new ComputerExecutionResult(DenialCode.StaleFrame);
            }
        }

        return null;
    }

    private static bool IsKeyboard(ComputerHostAction host) =>
        host.ActionClass is ComputerActionClass.Type or ComputerActionClass.Chord;

    private static bool IsPointer(InputPrimitive primitive) =>
        primitive is PointerMove or PointerButton or Scroll;

    private static IEnumerable<InputPrimitive> Expand(IReadOnlyList<InputPrimitive> primitives)
    {
        foreach (InputPrimitive primitive in primitives)
        {
            if (primitive is not TypeText text || text.Text.Length <= TypeRunLength)
            {
                yield return primitive;
                continue;
            }

            for (int offset = 0; offset < text.Text.Length; offset += TypeRunLength)
            {
                int length = Math.Min(TypeRunLength, text.Text.Length - offset);
                yield return new TypeText { Text = text.Text.Substring(offset, length) };
            }
        }
    }
}
