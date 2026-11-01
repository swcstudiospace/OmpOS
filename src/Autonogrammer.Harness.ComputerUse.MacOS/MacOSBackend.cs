using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.MacOS;

/// <summary>
/// Quartz backend. Injection is CGEvent posted to <c>kCGHIDEventTap</c>. Capture is the system
/// <c>screencapture</c> child. screencapture attribution is unverified until a Mac run (OQ-382).
/// Window bundle ids are unverified catalogue rows (OQ-388). Nothing here runs on Linux.
/// </summary>
public sealed class MacOSBackend : IComputerUseBackend
{
    /// <summary>Prefix on window identity until a Mac run verifies the bundle id (OQ-388).</summary>
    public const string UnverifiedIdentity = "Unverified";

    private readonly IScreenCaptureRunner _runner;

    private readonly string _stateDirectory;

    private readonly Lock _held = new();

    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    private int _buttons;

    /// <summary>Creates a backend that captures through <paramref name="runner"/>.</summary>
    /// <param name="runner">screencapture runner. The default starts <c>/usr/sbin/screencapture</c> only on macOS.</param>
    /// <param name="stateDirectory">Directory that owns capture files.</param>
    internal MacOSBackend(IScreenCaptureRunner runner, string stateDirectory)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        _runner = runner;
        _stateDirectory = stateDirectory;
    }

    /// <summary>Creates a backend with the default screencapture runner. The runner does not start a process until capture.</summary>
    public MacOSBackend()
        : this(new ScreencaptureProcessRunner(), "computer-use-capture")
    {
    }

    /// <inheritdoc/>
    public ComputerPlatform Platform => ComputerPlatform.MacOS;

    /// <inheritdoc/>
    public ComputerBackendCapabilities Capabilities =>
        ComputerBackendCapabilities.WindowEnumeration | ComputerBackendCapabilities.ClipboardText;

    /// <inheritdoc/>
    public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            return ValueTask.FromResult(ComputerAvailability.UnsupportedPlatform);
        }

        bool screen = MacOSInterop.PreflightScreenCaptureAccess();
        bool access = MacOSInterop.IsProcessTrusted();
        if (!screen || !access)
        {
            return ValueTask.FromResult(ComputerAvailability.PermissionMissing);
        }

        return ValueTask.FromResult(ComputerAvailability.Available);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool screen = OperatingSystem.IsMacOS() && MacOSInterop.PreflightScreenCaptureAccess();
        bool access = OperatingSystem.IsMacOS() && MacOSInterop.IsProcessTrusted();
        IReadOnlyList<ComputerPermissionStatus> statuses =
        [
            Status(ComputerPermissionKind.ScreenRecording, screen, "computer.permission.screen-recording"),
            Status(ComputerPermissionKind.Accessibility, access, "computer.permission.accessibility"),
        ];
        return ValueTask.FromResult(statuses);
    }

    /// <inheritdoc/>
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            return ValueTask.FromResult(Status(kind, false, MessageId(kind)));
        }

        if (kind == ComputerPermissionKind.ScreenRecording)
        {
            _ = MacOSInterop.RequestScreenCaptureAccess();
            return ValueTask.FromResult(Status(kind, MacOSInterop.PreflightScreenCaptureAccess(), MessageId(kind)));
        }

        return ValueTask.FromResult(Status(kind, MacOSInterop.IsProcessTrusted(), MessageId(kind)));
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        IReadOnlyList<DisplayInfo> displays = ReadDisplays();
        return ValueTask.FromResult(displays);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        IReadOnlyList<WindowInfo> windows = ReadWindows();
        return ValueTask.FromResult(windows);
    }

    /// <inheritdoc/>
    public ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        IntPtr evt = MacOSInterop.CreateEvent(IntPtr.Zero);
        if (evt == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        try
        {
            MacOSInterop.CgPoint point = MacOSInterop.GetLocation(evt);
            int buttons = 0;
            if (MacOSInterop.ButtonState(MacOSInterop.EventSourceHid, 0) != 0)
            {
                buttons |= 1;
            }

            if (MacOSInterop.ButtonState(MacOSInterop.EventSourceHid, 2) != 0)
            {
                buttons |= 2;
            }

            if (MacOSInterop.ButtonState(MacOSInterop.EventSourceHid, 1) != 0)
            {
                buttons |= 4;
            }

            return ValueTask.FromResult(new PointerState { X = (int)point.X, Y = (int)point.Y, Buttons = buttons });
        }
        finally
        {
            MacOSInterop.Release(evt);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(region);
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        string name = "frame-" + Guid.NewGuid().ToString("N") + ".png";
        ReadOnlyMemory<byte> png = await _runner.RunAsync(
            new ScreenCaptureRequest(_stateDirectory, name, ScreenCaptureArguments.Timeout),
            cancellationToken).ConfigureAwait(false);
        return new RawFrame { Pixels = png, Width = region.Bounds.Width, Height = region.Bounds.Height, Stride = region.Bounds.Width * 4 };
    }

    /// <inheritdoc/>
    public ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        if (!MacOSInterop.IsProcessTrusted())
        {
            throw new ComputerBackendException(ComputerFailure.PermissionMissing);
        }

        Post(primitive);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            return ValueTask.CompletedTask;
        }

        lock (_held)
        {
            if ((_buttons & 1) != 0)
            {
                PostMouse(MacOSInterop.LeftMouseUp, 0);
            }

            if ((_buttons & 2) != 0)
            {
                PostMouse(MacOSInterop.OtherMouseUp, 2);
            }

            if ((_buttons & 4) != 0)
            {
                PostMouse(MacOSInterop.RightMouseUp, 1);
            }

            foreach (string name in _keys)
            {
                PostKey(name, false);
            }

            _buttons = 0;
            _keys.Clear();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        return ValueTask.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        RequireMac();
        throw new ComputerBackendException(ComputerFailure.Unavailable);
    }

    private static void RequireMac()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }
    }

    private static ComputerPermissionStatus Status(ComputerPermissionKind kind, bool granted, string messageId)
    {
        return new ComputerPermissionStatus { Kind = kind, Granted = granted, MessageId = messageId };
    }

    private static string MessageId(ComputerPermissionKind kind)
    {
        return kind == ComputerPermissionKind.ScreenRecording
            ? "computer.permission.screen-recording"
            : "computer.permission.accessibility";
    }

    private static List<DisplayInfo> ReadDisplays()
    {
        uint[] ids = new uint[16];
        if (MacOSInterop.GetActiveDisplayList((uint)ids.Length, ids, out uint count) != 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        uint main = MacOSInterop.MainDisplayId();
        var list = new List<DisplayInfo>((int)count);
        for (int i = 0; i < count; i++)
        {
            MacOSInterop.CgRect bounds = MacOSInterop.DisplayBounds(ids[i]);
            double wide = MacOSInterop.DisplayPixelsWide(ids[i]);
            double high = MacOSInterop.DisplayPixelsHigh(ids[i]);
            double scale = bounds.Size.Width <= 0 ? 1 : wide / bounds.Size.Width;
            _ = high;
            var display = new DisplayInfo
            {
                Id = ids[i].ToString(CultureInfo.InvariantCulture),
                Bounds = new PixelRect
                {
                    X = (int)bounds.Origin.X,
                    Y = (int)bounds.Origin.Y,
                    Width = (int)bounds.Size.Width,
                    Height = (int)bounds.Size.Height,
                },
                ScaleFactor = scale,
                Primary = ids[i] == main,
            };
            NativePoint pixels = ScreenCaptureArguments.ToCapturePixels(display, new ModelPoint { X = 0, Y = 0 });
            list.Add(display with
            {
                Bounds = display.Bounds with { X = pixels.X, Y = pixels.Y, Width = (int)wide, Height = (int)high },
            });
        }

        return list;
    }

    private static List<WindowInfo> ReadWindows()
    {
        IntPtr list = MacOSInterop.WindowListCopyWindowInfo(MacOSInterop.WindowListOnScreen, 0);
        if (list == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            nint count = MacOSInterop.ArrayCount(list);
            var windows = new List<WindowInfo>((int)count);
            for (nint i = count - 1; i >= 0; i--)
            {
                IntPtr row = MacOSInterop.ArrayValue(list, i);
                windows.Add(DescribeWindow(row));
            }

            return windows;
        }
        finally
        {
            MacOSInterop.Release(list);
        }
    }

    private static WindowInfo DescribeWindow(IntPtr row)
    {
        int number = Number(row, "kCGWindowNumber");
        int pid = Number(row, "kCGWindowOwnerPID");
        string owner = Text(row, "kCGWindowOwnerName");
        string title = Text(row, "kCGWindowName");
        return new WindowInfo
        {
            Id = number.ToString(CultureInfo.InvariantCulture),
            Pid = pid,
            AppIdentity = UnverifiedIdentity + ":" + owner,
            Title = title,
            Bounds = new PixelRect { X = 0, Y = 0, Width = 0, Height = 0 },
            Minimized = false,
            Focused = false,
            IsOwnProcess = pid == Environment.ProcessId,
        };
    }

    private static int Number(IntPtr row, string key)
    {
        IntPtr name = Key(key);
        try
        {
            IntPtr value = MacOSInterop.DictionaryValue(row, name);
            if (value == IntPtr.Zero || !MacOSInterop.NumberGet(value, 9, out int number))
            {
                return 0;
            }

            return number;
        }
        finally
        {
            MacOSInterop.Release(name);
        }
    }

    private static string Text(IntPtr row, string key)
    {
        IntPtr name = Key(key);
        try
        {
            IntPtr value = MacOSInterop.DictionaryValue(row, name);
            if (value == IntPtr.Zero)
            {
                return string.Empty;
            }

            byte[] buffer = new byte[512];
            if (!MacOSInterop.StringGet(value, buffer, buffer.Length, MacOSInterop.StringEncodingUtf8))
            {
                return string.Empty;
            }

            int length = Array.IndexOf(buffer, (byte)0);
            return Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
        }
        finally
        {
            MacOSInterop.Release(name);
        }
    }

    private static IntPtr Key(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
        return MacOSInterop.StringCreate(IntPtr.Zero, bytes, MacOSInterop.StringEncodingUtf8);
    }

    private void Post(InputPrimitive primitive)
    {
        switch (primitive)
        {
            case PointerMove move:
                PostMove(move.X, move.Y);
                break;
            case PointerButton button:
                RememberButton(button);
                PostMouse(button.Down ? Down(button.Button) : Up(button.Button), (uint)Math.Max(0, button.Button - 1));
                break;
            case Scroll scroll:
                PostScroll(scroll.Button);
                break;
            case Key key:
                RememberKey(key);
                PostKey(key.Name, key.Down);
                break;
            case TypeText text:
                PostText(text.Text);
                break;
            default:
                throw new ComputerBackendException(ComputerFailure.Unavailable);
        }
    }

    private static void PostMove(int x, int y)
    {
        var point = new MacOSInterop.CgPoint { X = x, Y = y };
        IntPtr evt = MacOSInterop.CreateMouseEvent(IntPtr.Zero, MacOSInterop.MouseMoved, point, 0);
        PostAndRelease(evt);
    }

    private static void PostMouse(uint type, uint button)
    {
        IntPtr evt = MacOSInterop.CreateMouseEvent(IntPtr.Zero, type, default, button);
        PostAndRelease(evt);
    }

    private static void PostScroll(int button)
    {
        int wheel = button switch
        {
            4 => 1,
            5 => -1,
            _ => 0,
        };
        IntPtr evt = MacOSInterop.CreateScrollWheelEvent(IntPtr.Zero, MacOSInterop.ScrollEventUnitLine, 1, wheel);
        PostAndRelease(evt);
    }

    private static void PostKey(string name, bool down)
    {
        ushort key = string.Equals(name, "Escape", StringComparison.Ordinal) ? (ushort)MacOSInterop.VkEscape : (ushort)0;
        IntPtr evt = MacOSInterop.CreateKeyboardEvent(IntPtr.Zero, key, down);
        PostAndRelease(evt);
    }

    private static void PostText(string text)
    {
        IntPtr evt = MacOSInterop.CreateKeyboardEvent(IntPtr.Zero, 0, true);
        if (evt == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
        IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            MacOSInterop.KeyboardSetUnicodeString(evt, (nuint)text.Length, buffer);
            MacOSInterop.Post(MacOSInterop.HidEventTap, evt);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            MacOSInterop.Release(evt);
        }
    }

    private static void PostAndRelease(IntPtr evt)
    {
        if (evt == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        MacOSInterop.Post(MacOSInterop.HidEventTap, evt);
        MacOSInterop.Release(evt);
    }

    private static uint Down(int button)
    {
        return button switch
        {
            1 => MacOSInterop.LeftMouseDown,
            3 => MacOSInterop.RightMouseDown,
            _ => MacOSInterop.OtherMouseDown,
        };
    }

    private static uint Up(int button)
    {
        return button switch
        {
            1 => MacOSInterop.LeftMouseUp,
            3 => MacOSInterop.RightMouseUp,
            _ => MacOSInterop.OtherMouseUp,
        };
    }

    private void RememberButton(PointerButton button)
    {
        int bit = button.Button switch
        {
            1 => 1,
            2 => 2,
            3 => 4,
            _ => 0,
        };
        lock (_held)
        {
            if (button.Down)
            {
                _buttons |= bit;
            }
            else
            {
                _buttons &= ~bit;
            }
        }
    }

    private void RememberKey(Key key)
    {
        lock (_held)
        {
            if (key.Down)
            {
                _ = _keys.Add(key.Name);
            }
            else
            {
                _ = _keys.Remove(key.Name);
            }
        }
    }

    /// <summary>One screencapture invocation. The runner owns the process.</summary>
    /// <param name="StateDirectory">State directory.</param>
    /// <param name="FileName">Single path segment.</param>
    /// <param name="Timeout">Kill deadline.</param>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct ScreenCaptureRequest(string StateDirectory, string FileName, TimeSpan Timeout);

    /// <summary>Starts screencapture. Implementations must not use a shell.</summary>
    internal interface IScreenCaptureRunner
    {
        /// <summary>Runs one capture and returns the file bytes.</summary>
        /// <param name="request">Arguments and timeout.</param>
        /// <param name="cancellationToken">Kills the child when cancelled.</param>
        /// <returns>The captured file bytes.</returns>
        ValueTask<ReadOnlyMemory<byte>> RunAsync(ScreenCaptureRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Default runner. Starts <see cref="ScreenCaptureArguments.Executable"/> with
    /// <see cref="ProcessStartInfo.ArgumentList"/>, the allow-listed environment, a timeout and kill-on-cancel.
    /// Refuses to start on Linux.
    /// </summary>
    internal sealed class ScreencaptureProcessRunner : IScreenCaptureRunner
    {
        /// <inheritdoc/>
        public async ValueTask<ReadOnlyMemory<byte>> RunAsync(ScreenCaptureRequest request, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsMacOS())
            {
                throw new ComputerBackendException(ComputerFailure.Unavailable);
            }

            IReadOnlyList<string> args = ScreenCaptureArguments.Create(request.StateDirectory, request.FileName);
            var start = new ProcessStartInfo(ScreenCaptureArguments.Executable)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (string arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            start.Environment.Clear();
            foreach (KeyValuePair<string, string> pair in ScreenCaptureArguments.ScreenCaptureEnvironment(ReadEnvironment()))
            {
                start.Environment[pair.Key] = pair.Value;
            }

            using Process process = new() { StartInfo = start };
            if (!process.Start())
            {
                throw new ComputerBackendException(ComputerFailure.Unavailable);
            }

            using var timeout = new CancellationTokenSource(request.Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using CancellationTokenRegistration registration = linked.Token.Register(() => Kill(process));
            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Kill(process);
                throw new ComputerBackendException(ComputerFailure.Timeout);
            }

            string path = args[3];
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            File.Delete(path);
            return bytes;
        }

        private static Dictionary<string, string> ReadEnvironment()
        {
            var source = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    source[key] = value;
                }
            }

            return source;
        }

        private static void Kill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
