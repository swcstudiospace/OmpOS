using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// Kill-switch grab on a second X connection and thread. It never shares the injection connection.
/// <c>BadAccess</c> is <see cref="ComputerAvailability.KillSwitchUnavailable"/>.
/// </summary>
public sealed class X11HotkeyRegistrar : IGlobalHotkeyRegistrar, IDisposable
{
    /// <summary>Default chord when the caller passes a blank string.</summary>
    public const string DefaultChord = "Ctrl+Alt+Shift+Escape";

    private readonly X11BackendOptions _options;

    private readonly Lock _gate = new();

    private Thread? _thread;

    private IntPtr _display;

    private int _stop;

    private int _registered;

    /// <summary>Creates a registrar. <paramref name="options"/> selects the display and must not be the injection connection.</summary>
    /// <param name="options">Display name and environment reader.</param>
    public X11HotkeyRegistrar(X11BackendOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Raised on the hotkey thread when the grabbed chord is pressed.</summary>
    public event EventHandler? Pressed;

    /// <summary>Set when registration does not confirm. <c>BadAccess</c> maps to <see cref="ComputerAvailability.KillSwitchUnavailable"/>.</summary>
    public ComputerAvailability? RegistrationFailure { get; private set; }

    /// <inheritdoc/>
    public ValueTask<bool> RegisterAsync(string chord, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chord);
        cancellationToken.ThrowIfCancellationRequested();
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
                Name = "autonogrammer-x11-hotkey",
            };
            _thread.Start();
            return new ValueTask<bool>(done.Task.WaitAsync(cancellationToken));
        }
    }

    /// <inheritdoc/>
    public ValueTask UnregisterAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stop();
        return ValueTask.CompletedTask;
    }

    /// <summary>Closes the hotkey connection.</summary>
    public void Dispose()
    {
        Stop();
    }

    private void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            _stop = 1;
            thread = _thread;
            _registered = 0;
        }

        thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void Run(string chord, TaskCompletionSource<bool> done)
    {
        string? name = _options.ResolveDisplayName();
        _display = X11Interop.Open(name);
        if (_display == IntPtr.Zero)
        {
            RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
            _ = done.TrySetResult(false);
            return;
        }

        try
        {
            if (!Grab(chord))
            {
                RegistrationFailure = ComputerAvailability.KillSwitchUnavailable;
                _ = done.TrySetResult(false);
                return;
            }

            _registered = 1;
            RegistrationFailure = null;
            _ = done.TrySetResult(true);
            Pump(Keycode(chord));
        }
        finally
        {
            if (_display != IntPtr.Zero)
            {
                try
                {
                    _ = X11Interop.CloseDisplay(_display);
                }
                catch (IOException)
                {
                }

                _display = IntPtr.Zero;
            }
        }
    }

    private bool Grab(string chord)
    {
        int keycode = Keycode(chord);
        if (keycode == 0)
        {
            return false;
        }

        IntPtr root = X11Interop.RootWindow(_display, X11Interop.DefaultScreen(_display));
        uint modifiers = Modifiers(chord);
        uint[] locks =
        [
            0,
            X11Interop.LockMask,
            X11Interop.Mod2Mask,
            X11Interop.Mod5Mask,
            X11Interop.LockMask | X11Interop.Mod2Mask,
            X11Interop.LockMask | X11Interop.Mod5Mask,
            X11Interop.Mod2Mask | X11Interop.Mod5Mask,
            X11Interop.LockMask | X11Interop.Mod2Mask | X11Interop.Mod5Mask,
        ];
        foreach (uint extra in locks)
        {
            X11Interop.ClearError();
            _ = X11Interop.GrabKey(_display, keycode, modifiers | extra, root, 0, X11Interop.GrabModeAsync, X11Interop.GrabModeAsync);
            _ = X11Interop.Sync(_display, 0);
            if (X11Interop.LastError(_display) == X11Interop.BadAccess)
            {
                return false;
            }
        }

        return true;
    }

    private void Pump(int keycode)
    {
        IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(256);
        try
        {
            while (Volatile.Read(ref _stop) == 0)
            {
                while (Volatile.Read(ref _stop) == 0 && X11Interop.Pending(_display) > 0)
                {
                    _ = X11Interop.NextEvent(_display, buffer);
                    if (X11Interop.ReadEventType(buffer) == X11Interop.KeyPress
                        && X11Interop.ReadEventKeycode(buffer) == (uint)keycode)
                    {
                        Pressed?.Invoke(this, EventArgs.Empty);
                    }
                }

                Thread.Sleep(20);
            }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    private int Keycode(string chord)
    {
        string key = chord.Split('+').LastOrDefault(part => !IsModifier(part)) ?? "Escape";
        nuint keysym = X11Interop.StringToKeysym(KeyName(key));
        return keysym == 0 ? 0 : X11Interop.KeysymToKeycode(_display, keysym);
    }

    private static uint Modifiers(string chord)
    {
        uint mask = 0;
        foreach (string part in chord.Split('+'))
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                mask |= X11Interop.ControlMask;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                mask |= X11Interop.Mod1Mask;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                mask |= X11Interop.ShiftMask;
            }
        }

        return mask;
    }

    private static bool IsModifier(string part) =>
        part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
        || part.Equals("Control", StringComparison.OrdinalIgnoreCase)
        || part.Equals("Alt", StringComparison.OrdinalIgnoreCase)
        || part.Equals("Shift", StringComparison.OrdinalIgnoreCase);

    private static string KeyName(string name) => name switch
    {
        "Esc" or "Escape" => "Escape",
        _ => name,
    };
}
