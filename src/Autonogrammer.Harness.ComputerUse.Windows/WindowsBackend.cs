using System.Globalization;
using System.Runtime.InteropServices;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Windows;

/// <summary>
/// Win32 backend. Injection is SendInput. Capture is GDI BitBlt with <c>SRCCOPY</c> plus <c>CAPTUREBLT</c>.
/// Policy is not decided here. No entry point runs unless <see cref="OperatingSystem.IsWindows"/> is true.
/// Window identities are unverified catalogue rows (OQ-388).
/// </summary>
public sealed class WindowsBackend : IComputerUseBackend
{
    /// <summary>Prefix on <see cref="WindowInfo.AppIdentity"/> until a runner verifies the executable and signer (OQ-388).</summary>
    public const string UnverifiedIdentity = "Unverified";

    private readonly Lock _held = new();

    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    private int _buttons;

    /// <inheritdoc/>
    public ComputerPlatform Platform => ComputerPlatform.Windows;

    /// <inheritdoc/>
    public ComputerBackendCapabilities Capabilities =>
        ComputerBackendCapabilities.WindowEnumeration | ComputerBackendCapabilities.ClipboardText;

    /// <inheritdoc/>
    public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.FromResult(ComputerAvailability.UnsupportedPlatform);
        }

        return ValueTask.FromResult(DetectSecureDesktop() is null
            ? ComputerAvailability.Available
            : ComputerAvailability.SecureDesktop);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool secure = OperatingSystem.IsWindows() && DetectSecureDesktop() == DenialCode.SecureDesktop;
        bool elevated = OperatingSystem.IsWindows() && DetectElevatedTarget(Environment.ProcessId) == DenialCode.ElevatedTarget;
        IReadOnlyList<ComputerPermissionStatus> statuses =
        [
            Status(ComputerPermissionKind.Accessibility, !elevated, "computer.denial.elevated-target"),
            Status(ComputerPermissionKind.ScreenRecording, !secure, "computer.denial.secure-desktop"),
        ];
        return ValueTask.FromResult(statuses);
    }

    /// <inheritdoc/>
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string messageId = kind == ComputerPermissionKind.ScreenRecording
            ? "computer.denial.secure-desktop"
            : "computer.denial.elevated-target";
        return ValueTask.FromResult(Status(kind, granted: false, messageId));
    }

    /// <summary>
    /// UIPI check. Opens the process with <c>PROCESS_QUERY_LIMITED_INFORMATION</c> and reads
    /// <c>GetTokenInformation(TokenElevation)</c>. Returns <see cref="DenialCode.ElevatedTarget"/> when the token is elevated.
    /// Does not call the operating system on Linux.
    /// </summary>
    /// <param name="processId">Target process id.</param>
    /// <returns><see cref="DenialCode.ElevatedTarget"/>, or <see langword="null"/> when the target is not elevated.</returns>
    public static DenialCode? DetectElevatedTarget(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            return null;
        }

        IntPtr process = WindowsInterop.OpenProcess(WindowsInterop.ProcessQueryLimitedInformation, 0, (uint)processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            if (WindowsInterop.OpenProcessToken(process, 0x0008, out IntPtr token) == 0)
            {
                return null;
            }

            try
            {
                int read = WindowsInterop.GetTokenInformation(
                    token,
                    WindowsInterop.TokenElevation,
                    out WindowsInterop.TokenElevationInfo elevation,
                    (uint)Marshal.SizeOf<WindowsInterop.TokenElevationInfo>(),
                    out _);
                return read != 0 && elevation.TokenIsElevated != 0 ? DenialCode.ElevatedTarget : null;
            }
            finally
            {
                _ = WindowsInterop.CloseHandle(token);
            }
        }
        finally
        {
            _ = WindowsInterop.CloseHandle(process);
        }
    }

    /// <summary>
    /// Secure-desktop check. A failed <c>OpenInputDesktop</c> returns <see cref="DenialCode.SecureDesktop"/>.
    /// Does not call the operating system on Linux.
    /// </summary>
    /// <returns><see cref="DenialCode.SecureDesktop"/>, or <see langword="null"/> when the input desktop opened.</returns>
    public static DenialCode? DetectSecureDesktop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IntPtr desktop = WindowsInterop.OpenInputDesktop(0, 0, WindowsInterop.DesktopReadObjects);
        if (desktop == IntPtr.Zero)
        {
            return DenialCode.SecureDesktop;
        }

        _ = WindowsInterop.CloseDesktop(desktop);
        return null;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        IReadOnlyList<DisplayInfo> displays = ReadDisplays();
        return ValueTask.FromResult(displays);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        IReadOnlyList<WindowInfo> windows = ReadWindows();
        return ValueTask.FromResult(windows);
    }

    /// <inheritdoc/>
    public ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        if (WindowsInterop.GetCursorPos(out WindowsInterop.Point point) == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        int buttons = 0;
        if ((WindowsInterop.GetAsyncKeyState(0x01) & 0x8000) != 0)
        {
            buttons |= 1;
        }

        if ((WindowsInterop.GetAsyncKeyState(0x04) & 0x8000) != 0)
        {
            buttons |= 2;
        }

        if ((WindowsInterop.GetAsyncKeyState(0x02) & 0x8000) != 0)
        {
            buttons |= 4;
        }

        return ValueTask.FromResult(new PointerState { X = point.X, Y = point.Y, Buttons = buttons });
    }

    /// <inheritdoc/>
    public ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(region);
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        return ValueTask.FromResult(Capture(region.Bounds));
    }

    /// <inheritdoc/>
    public ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        DenialCode? secure = DetectSecureDesktop();
        if (secure == DenialCode.SecureDesktop)
        {
            throw new ComputerBackendException(ComputerFailure.SecureDesktop);
        }

        SendInputBuilder.InputRecord[] records = Build(primitive);
        Post(records);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return ValueTask.CompletedTask;
        }

        var records = new List<SendInputBuilder.InputRecord>();
        lock (_held)
        {
            if ((_buttons & 1) != 0)
            {
                records.Add(SendInputBuilder.PointerButton(1, false));
            }

            if ((_buttons & 2) != 0)
            {
                records.Add(SendInputBuilder.PointerButton(2, false));
            }

            if ((_buttons & 4) != 0)
            {
                records.Add(SendInputBuilder.PointerButton(3, false));
            }

            foreach (string name in _keys)
            {
                records.Add(SendInputBuilder.KeyScan(name, false));
            }

            _buttons = 0;
            _keys.Clear();
        }

        if (records.Count > 0)
        {
            Post(records);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        if (WindowsInterop.OpenClipboard(IntPtr.Zero) == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        try
        {
            if (WindowsInterop.IsClipboardFormatAvailable(WindowsInterop.CfUnicodeText) == 0)
            {
                return ValueTask.FromResult<string?>(null);
            }

            IntPtr data = WindowsInterop.GetClipboardData(WindowsInterop.CfUnicodeText);
            if (data == IntPtr.Zero)
            {
                return ValueTask.FromResult<string?>(null);
            }

            IntPtr locked = WindowsInterop.GlobalLock(data);
            if (locked == IntPtr.Zero)
            {
                return ValueTask.FromResult<string?>(null);
            }

            try
            {
                return ValueTask.FromResult<string?>(Marshal.PtrToStringUni(locked));
            }
            finally
            {
                _ = WindowsInterop.GlobalUnlock(data);
                _ = WindowsInterop.GlobalSize(data);
            }
        }
        finally
        {
            _ = WindowsInterop.CloseClipboard();
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        RequireWindows();
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
        IntPtr memory = WindowsInterop.GlobalAlloc(WindowsInterop.GmemMoveable, (nuint)bytes.Length);
        if (memory == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        IntPtr locked = WindowsInterop.GlobalLock(memory);
        if (locked == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        Marshal.Copy(bytes, 0, locked, bytes.Length);
        _ = WindowsInterop.GlobalUnlock(memory);
        if (WindowsInterop.OpenClipboard(IntPtr.Zero) == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        try
        {
            _ = WindowsInterop.EmptyClipboard();
            if (WindowsInterop.SetClipboardData(WindowsInterop.CfUnicodeText, memory) == IntPtr.Zero)
            {
                throw new ComputerBackendException(ComputerFailure.Unavailable);
            }

            memory = IntPtr.Zero;
        }
        finally
        {
            _ = WindowsInterop.CloseClipboard();
        }

        return ValueTask.CompletedTask;
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }
    }

    private static ComputerPermissionStatus Status(ComputerPermissionKind kind, bool granted, string messageId)
    {
        return new ComputerPermissionStatus { Kind = kind, Granted = granted, MessageId = messageId };
    }

    private static List<DisplayInfo> ReadDisplays()
    {
        IntPtr previous = PerMonitor();
        var found = new List<DisplayInfo>();
        try
        {
            WindowsInterop.MonitorEnumProc callback = (monitor, _, _, _) =>
            {
                var info = new WindowsInterop.MonitorInfo { Size = Marshal.SizeOf<WindowsInterop.MonitorInfo>() };
                if (WindowsInterop.GetMonitorInfo(monitor, ref info) != 0)
                {
                    found.Add(DescribeMonitor(info, found.Count));
                }

                return 1;
            };
            _ = WindowsInterop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        }
        finally
        {
            RestoreDpi(previous);
        }

        return found;
    }

    private static DisplayInfo DescribeMonitor(WindowsInterop.MonitorInfo info, int index)
    {
        int width = info.Monitor.Right - info.Monitor.Left;
        int height = info.Monitor.Bottom - info.Monitor.Top;
        return new DisplayInfo
        {
            Id = index.ToString(CultureInfo.InvariantCulture),
            Bounds = new PixelRect { X = info.Monitor.Left, Y = info.Monitor.Top, Width = width, Height = height },
            ScaleFactor = 1,
            Primary = (info.Flags & 1) != 0,
        };
    }

    private static int ReadDpi()
    {
        IntPtr dc = WindowsInterop.GetDc(IntPtr.Zero);
        try
        {
            return WindowsInterop.GetDeviceCaps(dc, WindowsInterop.LogPixelsX);
        }
        finally
        {
            _ = WindowsInterop.ReleaseDc(IntPtr.Zero, dc);
        }
    }

    private static List<WindowInfo> ReadWindows()
    {
        _ = ReadDpi();
        var found = new List<WindowInfo>();
        IntPtr foreground = WindowsInterop.GetForegroundWindow();
        WindowsInterop.EnumWindowsProc callback = (window, _) =>
        {
            if (WindowsInterop.IsWindowVisible(window) != 0)
            {
                found.Add(DescribeWindow(window, foreground));
            }

            return 1;
        };
        _ = WindowsInterop.EnumWindows(callback, IntPtr.Zero);
        found.Reverse();
        return found;
    }

    private static WindowInfo DescribeWindow(IntPtr window, IntPtr foreground)
    {
        _ = WindowsInterop.GetWindowThreadProcessId(window, out uint pid);
        var name = new char[256];
        _ = WindowsInterop.GetClassName(window, name, name.Length);
        string className = new string(name).TrimEnd('\0');
        int length = WindowsInterop.GetWindowTextLength(window);
        string title = string.Empty;
        if (length > 0)
        {
            var buffer = new char[length + 1];
            _ = WindowsInterop.GetWindowText(window, buffer, buffer.Length);
            title = new string(buffer).TrimEnd('\0');
        }

        WindowsInterop.Rect bounds = default;
        if (WindowsInterop.DwmGetWindowAttribute(window, WindowsInterop.DwmwaExtendedFrameBounds, out bounds, Marshal.SizeOf<WindowsInterop.Rect>()) != 0)
        {
            bounds = default;
        }

        return new WindowInfo
        {
            Id = window.ToInt64().ToString(CultureInfo.InvariantCulture),
            Pid = (int)pid,
            AppIdentity = UnverifiedIdentity + ":" + className,
            Title = title,
            Bounds = new PixelRect
            {
                X = bounds.Left,
                Y = bounds.Top,
                Width = Math.Max(0, bounds.Right - bounds.Left),
                Height = Math.Max(0, bounds.Bottom - bounds.Top),
            },
            Minimized = WindowsInterop.IsIconic(window) != 0,
            Focused = window == foreground,
            IsOwnProcess = pid == (uint)Environment.ProcessId,
        };
    }

    private static RawFrame Capture(PixelRect bounds)
    {
        IntPtr previous = PerMonitor();
        IntPtr screen = WindowsInterop.GetDc(IntPtr.Zero);
        IntPtr memory = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previousBitmap = IntPtr.Zero;
        try
        {
            memory = WindowsInterop.CreateCompatibleDc(screen);
            bitmap = WindowsInterop.CreateCompatibleBitmap(screen, bounds.Width, bounds.Height);
            previousBitmap = WindowsInterop.SelectObject(memory, bitmap);
            uint rop = WindowsInterop.SrcCopy | WindowsInterop.CaptureBlt;
            if (WindowsInterop.BitBlt(memory, 0, 0, bounds.Width, bounds.Height, screen, bounds.X, bounds.Y, rop) == 0)
            {
                throw new ComputerBackendException(ComputerFailure.Unavailable);
            }

            var info = new WindowsInterop.BitmapInfo
            {
                Header = new WindowsInterop.BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<WindowsInterop.BitmapInfoHeader>(),
                    Width = bounds.Width,
                    Height = -bounds.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = WindowsInterop.BiRgb,
                },
            };
            byte[] pixels = new byte[bounds.Width * bounds.Height * 4];
            if (WindowsInterop.GetDiBits(memory, bitmap, 0, (uint)bounds.Height, pixels, ref info, WindowsInterop.DibRgbColors) == 0)
            {
                throw new ComputerBackendException(ComputerFailure.Unavailable);
            }

            return new RawFrame { Pixels = pixels, Width = bounds.Width, Height = bounds.Height, Stride = bounds.Width * 4 };
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero)
            {
                _ = WindowsInterop.SelectObject(memory, previousBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                _ = WindowsInterop.DeleteObject(bitmap);
            }

            if (memory != IntPtr.Zero)
            {
                _ = WindowsInterop.DeleteDc(memory);
            }

            _ = WindowsInterop.ReleaseDc(IntPtr.Zero, screen);
            RestoreDpi(previous);
        }
    }

    private SendInputBuilder.InputRecord[] Build(InputPrimitive primitive)
    {
        switch (primitive)
        {
            case PointerMove move:
                return [SendInputBuilder.MoveAbsolute(move.X, move.Y, 0, 0, 65536, 65536)];
            case PointerButton button:
                RememberButton(button);
                return [SendInputBuilder.PointerButton(button.Button, button.Down)];
            case Scroll scroll:
                return [SendInputBuilder.ScrollClick(scroll.Button)];
            case Key key:
                RememberKey(key);
                return [SendInputBuilder.KeyScan(key.Name, key.Down)];
            case TypeText text:
                return SendInputBuilder.EncodeText(text.Text);
            default:
                throw new ComputerBackendException(ComputerFailure.Unavailable);
        }
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

    private static void Post(IReadOnlyList<SendInputBuilder.InputRecord> records)
    {
        var native = new WindowsInterop.InputNative[records.Count];
        for (int i = 0; i < records.Count; i++)
        {
            SendInputBuilder.InputRecord record = records[i];
            native[i] = new WindowsInterop.InputNative
            {
                Type = record.Type,
                Dx = record.Dx,
                Dy = record.Dy,
                MouseData = record.MouseData,
                Flags = record.Flags,
                Scan = record.Scan,
            };
        }

        uint sent = WindowsInterop.SendInput((uint)native.Length, native, Marshal.SizeOf<WindowsInterop.InputNative>());
        if (sent != (uint)native.Length)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }
    }

    private static IntPtr PerMonitor()
    {
        _ = WindowsInterop.SetProcessDpiAwarenessContext(WindowsInterop.PerMonitorV2);
        return WindowsInterop.PerMonitorV2;
    }

    private static void RestoreDpi(IntPtr previous)
    {
        if (previous != IntPtr.Zero)
        {
            _ = WindowsInterop.SetProcessDpiAwarenessContext(previous);
        }
    }
}
