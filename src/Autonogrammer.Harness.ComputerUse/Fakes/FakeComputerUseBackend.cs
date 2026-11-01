using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Fakes;

/// <summary>
/// Thread-safe fake backend. Gate tests use this instead of a display.
/// It is product code so the executor can be tested without an OS backend project reference.
/// </summary>
public sealed class FakeComputerUseBackend : IComputerUseBackend, Policy.IComputerObservation
{
    private readonly Lock _gate = new();
    private ComputerAvailability _availability = ComputerAvailability.Available;
    private IReadOnlyList<ComputerPermissionStatus> _permissions = [];
    private IReadOnlyList<DisplayInfo> _displays = [];
    private IReadOnlyList<WindowInfo> _windows = [];
    private PointerState _pointer = new() { X = 0, Y = 0, Buttons = 0 };
    private int _injectCount;
    private InputPrimitive? _lastInjected;
    private int _releaseCount;
    private int _listWindowsCount;
    private int _failListWindowsAfter = int.MaxValue;

    /// <inheritdoc/>
    public ComputerPlatform Platform => ComputerPlatform.X11;

    /// <inheritdoc/>
    public ComputerBackendCapabilities Capabilities => ComputerBackendCapabilities.WindowEnumeration;

    /// <summary>How many times <see cref="InjectAsync"/> ran.</summary>
    public int InjectCount
    {
        get
        {
            lock (_gate)
            {
                return _injectCount;
            }
        }
    }

    /// <summary>Last injected primitive.</summary>
    public InputPrimitive? LastInjected
    {
        get
        {
            lock (_gate)
            {
                return _lastInjected;
            }
        }
    }

    /// <summary>How many times <see cref="ReleaseAllInputAsync"/> ran.</summary>
    public int ReleaseCount
    {
        get
        {
            lock (_gate)
            {
                return _releaseCount;
            }
        }
    }

    /// <summary>Sets availability.</summary>
    /// <param name="availability">The state.</param>
    public void SetAvailability(ComputerAvailability availability)
    {
        lock (_gate)
        {
            _availability = availability;
        }
    }

    /// <summary>Sets permission facts.</summary>
    /// <param name="permissions">The facts.</param>
    public void SetPermissions(IReadOnlyList<ComputerPermissionStatus> permissions)
    {
        lock (_gate)
        {
            _permissions = permissions;
        }
    }

    /// <summary>Sets the scripted window list.</summary>
    /// <param name="windows">Windows, bottom to top.</param>
    public void SetWindows(IReadOnlyList<WindowInfo> windows)
    {
        lock (_gate)
        {
            _windows = windows;
        }
    }

    /// <summary>Sets the pointer.</summary>
    /// <param name="state">The pointer.</param>
    public void SetPointer(PointerState state)
    {
        lock (_gate)
        {
            _pointer = state;
        }
    }

    /// <summary>Sets displays.</summary>
    /// <param name="displays">The displays.</param>
    public void SetDisplays(IReadOnlyList<DisplayInfo> displays)
    {
        lock (_gate)
        {
            _displays = displays;
        }
    }

    /// <summary>Fails <see cref="ListWindowsAsync"/> after <paramref name="successfulCalls"/> successes.</summary>
    /// <param name="successfulCalls">Calls that still succeed.</param>
    public void FailListWindowsAfter(int successfulCalls)
    {
        lock (_gate)
        {
            _failListWindowsAfter = successfulCalls;
        }
    }

    /// <inheritdoc/>
    public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_availability);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_permissions);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new ComputerPermissionStatus
        {
            Kind = kind,
            Granted = true,
            MessageId = "computer.permission.screen-recording",
        });
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_displays);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _listWindowsCount++;
            if (_listWindowsCount > _failListWindowsAfter)
            {
                throw new InvalidOperationException("Scripted window-list failure.");
            }

            return ValueTask.FromResult(_windows);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_pointer);
        }
    }

    /// <inheritdoc/>
    public ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new RawFrame
        {
            Pixels = new byte[4],
            Width = 1,
            Height = 1,
            Stride = 4,
        });
    }

    /// <inheritdoc/>
    public ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _injectCount++;
            _lastInjected = primitive;
            if (primitive is PointerMove move)
            {
                _pointer = _pointer with { X = move.X, Y = move.Y };
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _releaseCount++;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc/>
    public ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
