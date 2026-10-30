using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.MacOS;

/// <summary>
/// Kill-switch chord via Carbon <c>RegisterEventHotKey</c>. It does not share the injection event source.
/// On Linux the registrar returns false and does not call Carbon.
/// </summary>
public sealed class MacOSHotkeyRegistrar : IGlobalHotkeyRegistrar, IDisposable
{
    /// <summary>Default chord when the caller passes a blank string.</summary>
    public const string DefaultChord = "Ctrl+Alt+Shift+Escape";

    private readonly Lock _gate = new();

    private Thread? _thread;

    private int _stop;

    private int _registered;

    private IntPtr _hotKey;

    private IntPtr _handler;

    /// <summary>Raised on the hotkey thread when the chord is pressed.</summary>
    public event EventHandler? Pressed;

    /// <summary>Set when registration does not confirm.</summary>
    public ComputerAvailability? RegistrationFailure { get; private set; }

    /// <inheritdoc/>
    public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chord);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            return ValueTask.FromResult(false);
        }

        lock (_gate)
        {
            if (_registered != 0)
            {
                RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
                return ValueTask.FromResult(false);
            }

            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            string requested = string.IsNullOrWhiteSpace(chord) ? DefaultChord : chord;
            _stop = 0;
            _thread = new Thread(() => Run(requested, done))
            {
                IsBackground = true,
                Name = "autonogrammer-macos-hotkey",
            };
            _thread.Start();
            return new ValueTask<bool>(done.Task.WaitAsync(cancellationToken));
        }
    }

    /// <inheritdoc/>
    public ValueTask UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Thread? thread;
        lock (_gate)
        {
            _stop = 1;
            thread = _thread;
            _registered = 0;
        }

        thread?.Join(TimeSpan.FromSeconds(2));
        return ValueTask.CompletedTask;
    }

    /// <summary>Stops the Carbon event thread.</summary>
    public void Dispose()
    {
        _stop = 1;
        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void Run(string chord, TaskCompletionSource<bool> done)
    {
        if (!TryParse(chord, out uint modifiers, out uint keyCode))
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            done.TrySetResult(false);
            return;
        }

        IntPtr target = MacOSInterop.GetEventDispatcherTarget();
        MacOSInterop.EventHandler callback = (_, _, _) =>
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return 0;
        };
        var id = new MacOSInterop.HotKeyId { Signature = 0x41474B48, Id = 1 };
        if (MacOSInterop.InstallEventHandler(target, callback, 0, IntPtr.Zero, IntPtr.Zero, out _handler) != 0
            || MacOSInterop.RegisterEventHotKey(keyCode, modifiers, id, target, 0, out _hotKey) != 0)
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            done.TrySetResult(false);
            return;
        }

        _registered = 1;
        done.TrySetResult(true);
        while (_stop == 0)
        {
            int status = MacOSInterop.ReceiveNextEvent(0, IntPtr.Zero, 0.2, true, out IntPtr evt);
            if (status == 0 && evt != IntPtr.Zero)
            {
                MacOSInterop.Release(evt);
            }
        }

        if (_hotKey != IntPtr.Zero)
        {
            _ = MacOSInterop.UnregisterEventHotKey(_hotKey);
            _hotKey = IntPtr.Zero;
        }

        if (_handler != IntPtr.Zero)
        {
            _ = MacOSInterop.RemoveEventHandler(_handler);
            _handler = IntPtr.Zero;
        }

        _registered = 0;
    }

    private static bool TryParse(string chord, out uint modifiers, out uint keyCode)
    {
        modifiers = 0;
        keyCode = 0;
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string part in parts)
        {
            switch (part)
            {
                case "Ctrl":
                case "Control":
                    modifiers |= MacOSInterop.ControlKey;
                    break;
                case "Alt":
                case "Option":
                    modifiers |= MacOSInterop.OptionKey;
                    break;
                case "Shift":
                    modifiers |= MacOSInterop.ShiftKey;
                    break;
                case "Cmd":
                case "Meta":
                    modifiers |= MacOSInterop.CmdKey;
                    break;
                case "Escape":
                    keyCode = MacOSInterop.VkEscape;
                    break;
                default:
                    return false;
            }
        }

        return keyCode != 0;
    }
}
