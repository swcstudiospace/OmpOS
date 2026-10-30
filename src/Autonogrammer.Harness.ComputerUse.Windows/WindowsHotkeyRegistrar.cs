using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Windows;

/// <summary>
/// Kill-switch chord via <c>RegisterHotKey</c> on a message-only thread. It does not share the injection call.
/// On Linux the registrar returns false and does not call user32.
/// </summary>
public sealed class WindowsHotkeyRegistrar : IGlobalHotkeyRegistrar, IDisposable
{
    /// <summary>Default chord when the caller passes a blank string.</summary>
    public const string DefaultChord = "Ctrl+Alt+Shift+Escape";

    private readonly Lock _gate = new();

    private Thread? _thread;

    private IntPtr _window;

    private int _stop;

    private int _registered;

    /// <summary>Raised on the hotkey thread when the chord is pressed.</summary>
    public event EventHandler? Pressed;

    /// <summary>Set when registration does not confirm.</summary>
    public ComputerAvailability? RegistrationFailure { get; private set; }

    /// <inheritdoc/>
    public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chord);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
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
                Name = "autonogrammer-win32-hotkey",
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
        IntPtr window;
        lock (_gate)
        {
            _stop = 1;
            window = _window;
            thread = _thread;
            _registered = 0;
        }

        if (OperatingSystem.IsWindows() && window != IntPtr.Zero)
        {
            _ = WindowsInterop.PostMessage(window, WindowsInterop.WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        thread?.Join(TimeSpan.FromSeconds(2));
        return ValueTask.CompletedTask;
    }

    /// <summary>Stops the message thread.</summary>
    public void Dispose()
    {
        _stop = 1;
        if (OperatingSystem.IsWindows() && _window != IntPtr.Zero)
        {
            _ = WindowsInterop.PostMessage(_window, WindowsInterop.WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void Run(string chord, TaskCompletionSource<bool> done)
    {
        if (!TryParse(chord, out uint modifiers, out uint virtualKey))
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            done.TrySetResult(false);
            return;
        }

        IntPtr window = WindowsInterop.CreateWindowEx(
            0,
            "Message",
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            WindowsInterop.HwndMessage,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        _window = window;
        if (window == IntPtr.Zero || WindowsInterop.RegisterHotKey(window, 1, modifiers, virtualKey) == 0)
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            done.TrySetResult(false);
            return;
        }

        _registered = 1;
        done.TrySetResult(true);
        while (_stop == 0 && WindowsInterop.GetMessage(out WindowsInterop.Message message, window, 0, 0) > 0)
        {
            if (message.Id == WindowsInterop.WmHotkey)
            {
                Pressed?.Invoke(this, EventArgs.Empty);
            }

            _ = WindowsInterop.TranslateMessage(ref message);
            _ = WindowsInterop.DispatchMessage(ref message);
        }

        _ = WindowsInterop.UnregisterHotKey(window, 1);
        _ = WindowsInterop.DestroyWindow(window);
        _window = IntPtr.Zero;
        _registered = 0;
    }

    private static bool TryParse(string chord, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (string part in parts)
        {
            switch (part)
            {
                case "Ctrl":
                case "Control":
                    modifiers |= WindowsInterop.ModControl;
                    break;
                case "Alt":
                    modifiers |= WindowsInterop.ModAlt;
                    break;
                case "Shift":
                    modifiers |= WindowsInterop.ModShift;
                    break;
                case "Win":
                case "Meta":
                    modifiers |= WindowsInterop.ModWin;
                    break;
                case "Escape":
                    virtualKey = 0x1B;
                    break;
                default:
                    return false;
            }
        }

        return virtualKey != 0;
    }
}
