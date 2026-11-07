using System.Globalization;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// X11 backend. Capture is <c>XGetImage</c> on the root. Input is XTest. Windows are EWMH with an
/// <c>XQueryTree</c> fallback. Policy is not decided here.
/// </summary>
public sealed class X11Backend : IComputerUseBackend, IDisposable
{
    /// <summary>Identity used when <c>/proc/&lt;pid&gt;/exe</c> cannot be read. The gate treats this as ungranted.</summary>
    public const string UnknownAppIdentity = "unknown";

    private readonly X11BackendOptions _options;

    private readonly HashSet<uint> _keysDown = [];

    private readonly HashSet<uint> _buttonsDown = [];

    private X11Connection? _connection;

    private string _stackingSource = "EWMH";

    /// <summary>Creates a backend. <paramref name="options"/> may be omitted to read the head process environment.</summary>
    /// <param name="options">Display, environment, clipboard, clock and log.</param>
    public X11Backend(X11BackendOptions? options = null)
    {
        _options = options ?? new X11BackendOptions();
        if (_options.Environment is null || _options.Time is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
    }

    /// <inheritdoc/>
    public ComputerPlatform Platform => ComputerPlatform.X11;

    /// <inheritdoc/>
    public ComputerBackendCapabilities Capabilities =>
        ComputerBackendCapabilities.WindowEnumeration | ComputerBackendCapabilities.ClipboardText;

    /// <summary><c>EWMH</c> or <c>XQueryTree</c> from the last window list.</summary>
    public string StackingSource => _stackingSource;

    /// <inheritdoc/>
    public async ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Func<string, string?> read = _options.SessionReader();
        if (WaylandDetection.IsWayland(read))
        {
            return ComputerAvailability.WaylandUnsupported;
        }

        if (string.IsNullOrEmpty(read("DISPLAY")))
        {
            return ComputerAvailability.NoDisplay;
        }

        X11Connection connection = EnsureConnection();
        if (!connection.IsOpen)
        {
            return ComputerAvailability.NoDisplay;
        }

        bool test = await connection.InvokeAsync(X11Interop.QueryTest, cancellationToken).ConfigureAwait(false);
        return test
            ? ComputerAvailability.Available
            : ComputerAvailability.InputInjectionUnavailable;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ComputerPermissionStatus> statuses =
        [
            Granted(ComputerPermissionKind.ScreenRecording, "computer.permission.screen-recording"),
            Granted(ComputerPermissionKind.Accessibility, "computer.permission.accessibility"),
        ];
        return ValueTask.FromResult(statuses);
    }

    /// <inheritdoc/>
    public ValueTask<ComputerPermissionStatus> RequestPermissionAsync(ComputerPermissionKind kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string id = kind == ComputerPermissionKind.Accessibility
            ? "computer.permission.accessibility"
            : "computer.permission.screen-recording";
        return ValueTask.FromResult(Granted(kind, id));
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DisplayInfo>> ListDisplaysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await RequireOpen().InvokeAsync(ReadDisplays, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await RequireOpen().InvokeAsync(ReadWindows, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<PointerState> GetPointerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await RequireOpen().InvokeAsync(ReadPointer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<RawFrame> CaptureAsync(CaptureRegion region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(region);
        cancellationToken.ThrowIfCancellationRequested();
        return await RequireOpen().InvokeAsync(display => Capture(display, region), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask InjectAsync(InputPrimitive primitive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        cancellationToken.ThrowIfCancellationRequested();
        await RequireOpen().InvokeAsync(display => Inject(display, primitive), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseAllInputAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await RequireOpen().InvokeAsync(ReleaseAll, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<string?> ReadClipboardTextAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IClipboardTextPort clipboard = _options.Clipboard ?? throw new ComputerBackendException(ComputerFailure.Unavailable);
        return clipboard.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask WriteClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        IClipboardTextPort clipboard = _options.Clipboard ?? throw new ComputerBackendException(ComputerFailure.Unavailable);
        return clipboard.WriteAsync(text, cancellationToken);
    }

    /// <summary>Closes the injection connection. Does not close the hotkey connection.</summary>
    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }

    private X11Connection EnsureConnection()
    {
        _connection ??= new X11Connection(_options.ResolveDisplayName());
        return _connection;
    }

    private X11Connection RequireOpen()
    {
        X11Connection connection = EnsureConnection();
        if (!connection.IsOpen)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        return connection;
    }

    private IReadOnlyList<DisplayInfo> ReadDisplays(IntPtr display)
    {
        int screen = X11Interop.DefaultScreen(display);
        IntPtr root = X11Interop.RootWindow(display, screen);
        X11Interop.ClearError();
        IntPtr monitors = X11Interop.GetMonitors(display, root, 1, out int count);
        if (monitors != IntPtr.Zero && count > 0 && X11Interop.LastError(display) == 0)
        {
            var list = new DisplayInfo[count];
            for (int i = 0; i < count; i++)
            {
                X11Interop.MonitorView monitor = X11Interop.ReadMonitor(monitors, i);
                list[i] = new DisplayInfo
                {
                    Id = monitor.Name.ToString(CultureInfo.InvariantCulture),
                    Bounds = new PixelRect { X = monitor.X, Y = monitor.Y, Width = monitor.Width, Height = monitor.Height },
                    ScaleFactor = 1,
                    Primary = monitor.Primary || count == 1,
                };
            }

            X11Interop.FreeMonitors(monitors);
            return list;
        }

        if (monitors != IntPtr.Zero)
        {
            X11Interop.FreeMonitors(monitors);
        }

        return
        [
            new DisplayInfo
            {
                Id = screen.ToString(CultureInfo.InvariantCulture),
                Bounds = new PixelRect
                {
                    X = 0,
                    Y = 0,
                    Width = X11Interop.DisplayWidth(display, screen),
                    Height = X11Interop.DisplayHeight(display, screen),
                },
                ScaleFactor = 1,
                Primary = true,
            },
        ];
    }

    private IReadOnlyList<WindowInfo> ReadWindows(IntPtr display)
    {
        int screen = X11Interop.DefaultScreen(display);
        IntPtr root = X11Interop.RootWindow(display, screen);
        IntPtr stacking = X11Interop.Intern(display, "_NET_CLIENT_LIST_STACKING");
        long[]? clients = ReadWindowList(display, root, stacking);
        _stackingSource = clients is null ? "XQueryTree" : "EWMH";
        if (clients is null)
        {
            if (X11Interop.QueryTree(display, root, out _, out _, out IntPtr children, out uint count) == 0 || children == IntPtr.Zero)
            {
                return [];
            }

            try
            {
                clients = new long[count];
                for (uint i = 0; i < count; i++)
                {
                    clients[i] = MarshalReadWindow(children, i);
                }
            }
            finally
            {
                _ = X11Interop.Free(children);
            }
        }

        IntPtr activeAtom = X11Interop.Intern(display, "_NET_ACTIVE_WINDOW");
        long active = ReadWindowList(display, root, activeAtom)?.FirstOrDefault() ?? 0;
        IntPtr nameAtom = X11Interop.Intern(display, "_NET_WM_NAME");
        IntPtr utf8 = X11Interop.Intern(display, "UTF8_STRING");
        IntPtr wmName = X11Interop.Intern(display, "WM_NAME");
        IntPtr wmClass = X11Interop.Intern(display, "WM_CLASS");
        IntPtr pidAtom = X11Interop.Intern(display, "_NET_WM_PID");
        var windows = new List<WindowInfo>(clients.Length);
        IntPtr attributes = MarshalAlloc(256);
        try
        {
            foreach (long id in clients)
            {
                WindowInfo? described = DescribeWindow(display, root, attributes, id, active, nameAtom, utf8, wmName, wmClass, pidAtom);
                if (described is not null)
                {
                    windows.Add(described);
                }
            }
        }
        finally
        {
            MarshalFree(attributes);
        }

        return windows;
    }
    private static WindowInfo? DescribeWindow(
        IntPtr display,
        IntPtr root,
        IntPtr attributes,
        long id,
        long active,
        IntPtr nameAtom,
        IntPtr utf8,
        IntPtr wmName,
        IntPtr wmClass,
        IntPtr pidAtom)
    {
        IntPtr window = new(id);
        if (X11Interop.GetWindowAttributes(display, window, attributes) == 0)
        {
            return null;
        }

        X11Interop.ReadWindowOrigin(attributes, out int x, out int y, out int width, out int height);
        if (X11Interop.TranslateCoordinates(display, window, root, 0, 0, out int rootX, out int rootY, out _) != 0)
        {
            x = rootX;
            y = rootY;
        }

        int pid = ReadCardinal(display, window, pidAtom);
        string title = ReadUtf8Property(display, window, nameAtom, utf8) ?? ReadLatin1Property(display, window, wmName) ?? string.Empty;
        return new WindowInfo
        {
            Id = id.ToString("x", CultureInfo.InvariantCulture),
            Pid = pid,
            AppIdentity = Identity(ReadClass(display, window, wmClass), pid),
            Title = title,
            Bounds = new PixelRect { X = x, Y = y, Width = width, Height = height },
            Minimized = X11Interop.ReadMapState(attributes) == X11Interop.IsUnmapped,
            Focused = id == active,
            IsOwnProcess = pid == Environment.ProcessId,
        };
    }


    private PointerState ReadPointer(IntPtr display)
    {
        IntPtr root = X11Interop.RootWindow(display, X11Interop.DefaultScreen(display));
        if (X11Interop.QueryPointer(display, root, out _, out _, out int x, out int y, out _, out _, out uint mask) == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        int buttons = 0;
        if ((mask & X11Interop.Button1Mask) != 0)
        {
            buttons |= 1;
        }

        if ((mask & X11Interop.Button2Mask) != 0)
        {
            buttons |= 2;
        }

        if ((mask & X11Interop.Button3Mask) != 0)
        {
            buttons |= 4;
        }

        return new PointerState { X = x, Y = y, Buttons = buttons };
    }

    private RawFrame Capture(IntPtr display, CaptureRegion region)
    {
        if (region.Bounds.Width <= 0 || region.Bounds.Height <= 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        int originX = region.Bounds.X;
        int originY = region.Bounds.Y;
        if (!string.IsNullOrEmpty(region.DisplayId))
        {
            DisplayInfo? match = ReadDisplays(display).FirstOrDefault(item => string.Equals(item.Id, region.DisplayId, StringComparison.Ordinal));
            if (match is null)
            {
                throw new ComputerBackendException(ComputerFailure.TargetGone);
            }

            originX += match.Bounds.X;
            originY += match.Bounds.Y;
        }

        IntPtr root = X11Interop.RootWindow(display, X11Interop.DefaultScreen(display));
        IntPtr image = X11Interop.GetImage(
            display,
            root,
            originX,
            originY,
            (uint)region.Bounds.Width,
            (uint)region.Bounds.Height,
            X11Interop.AllPlanes,
            X11Interop.ZPixmap);
        if (image == IntPtr.Zero)
        {
            throw new ComputerBackendException(ComputerFailure.TargetGone);
        }

        try
        {
            return CopyBgra(X11Interop.ReadImage(image));
        }
        finally
        {
            X11Interop.DestroyImage(image);
        }
    }

    private void Inject(IntPtr display, InputPrimitive primitive)
    {
        switch (primitive)
        {
            case PointerMove move:
                MovePointer(display, move.X, move.Y);
                break;
            case PointerButton button:
                ClickButton(display, button);
                break;
            case Scroll scroll:
                ScrollButton(display, scroll.Button);
                break;
            case Key key:
                SendKey(display, key);
                break;
            case TypeText text:
                var scratches = new List<ScratchMapping>();
                bool restoreFailed = false;
                try
                {
                    foreach (char value in text.Text)
                    {
                        TypeCharacter(display, value, scratches);
                    }

                    if (scratches.Count > 0)
                    {
                        _ = X11Interop.Sync(display, 0);
                        Thread.Sleep(200);
                    }
                }
                finally
                {
                    restoreFailed = RestoreScratches(display, scratches);
                }

                if (restoreFailed)
                {
                    _options.Log?.Invoke(_options.Time.GetUtcNow().ToString("o", CultureInfo.InvariantCulture) + " Computer Use keyboard map restore failed.");
                    throw new ComputerBackendException("Computer Use keyboard map restore failed.");
                }

                break;
            default:
                throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        _ = X11Interop.Sync(display, 0);
    }

    private void MovePointer(IntPtr display, int x, int y)
    {
        PointerState start = ReadPointer(display);
        int dx = x - start.X;
        int dy = y - start.Y;
        int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int steps = distance == 0 ? 1 : Math.Min(8, distance);
        int screen = X11Interop.DefaultScreen(display);
        for (int step = 1; step <= steps; step++)
        {
            int nextX = start.X + (dx * step / steps);
            int nextY = start.Y + (dy * step / steps);
            _ = X11Interop.FakeMotion(display, screen, nextX, nextY, 0);
        }
    }

    private void ClickButton(IntPtr display, PointerButton button)
    {
        if (button.Button is < 1 or > 3)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        uint which = (uint)button.Button;
        _ = X11Interop.FakeButton(display, which, button.Down ? 1 : 0, 0);
        if (button.Down)
        {
            _ = _buttonsDown.Add(which);
        }
        else
        {
            _ = _buttonsDown.Remove(which);
        }
    }

    private static void ScrollButton(IntPtr display, int button)
    {
        if (button is < 4 or > 7)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        _ = X11Interop.FakeButton(display, (uint)button, 1, 0);
        _ = X11Interop.FakeButton(display, (uint)button, 0, 0);
    }

    private void SendKey(IntPtr display, Key key)
    {
        nuint keysym = X11Interop.StringToKeysym(KeysymName(key.Name));
        if (keysym == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        byte keycode = X11Interop.KeysymToKeycode(display, keysym);
        if (keycode == 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        _ = X11Interop.FakeKey(display, keycode, key.Down ? 1 : 0, 0);
        if (key.Down)
        {
            _ = _keysDown.Add(keycode);
        }
        else
        {
            _ = _keysDown.Remove(keycode);
        }
    }

    private static void TypeCharacter(IntPtr display, char value, List<ScratchMapping> scratches)
    {
        uint keysym = KeysymMapper.ToKeysym(value);
        byte keycode = X11Interop.KeysymToKeycode(display, keysym);
        if (keycode != 0)
        {
            PressMapped(display, keycode, keysym);
            return;
        }

        byte scratch = (byte)(255 - scratches.Count);
        IntPtr saved = X11Interop.GetKeyboardMapping(display, scratch, 1, out int per);
        if (saved == IntPtr.Zero || per <= 0)
        {
            throw new ComputerBackendException(ComputerFailure.Unavailable);
        }

        IntPtr replacement = MarshalAlloc(per * IntPtr.Size);
        for (int i = 0; i < per; i++)
        {
            WriteKeysym(replacement, i, i == 0 ? keysym : 0);
        }

        _ = X11Interop.ChangeKeyboardMapping(display, scratch, per, replacement, 1);
        _ = X11Interop.Sync(display, 0);
        MarshalFree(replacement);
        Tap(display, scratch);
        scratches.Add(new ScratchMapping(scratch, per, saved));
    }

    private static bool RestoreScratches(IntPtr display, List<ScratchMapping> scratches)
    {
        bool failed = false;
        foreach (ScratchMapping scratch in scratches)
        {
            X11Interop.ClearError();
            _ = X11Interop.ChangeKeyboardMapping(display, scratch.Keycode, scratch.Per, scratch.Saved, 1);
            _ = X11Interop.Sync(display, 0);
            failed |= X11Interop.LastError(display) != 0;
            _ = X11Interop.Free(scratch.Saved);
        }

        return failed;
    }
    private sealed class ScratchMapping
    {
        internal ScratchMapping(byte keycode, int per, IntPtr saved)
        {
            Keycode = keycode;
            Per = per;
            Saved = saved;
        }

        internal byte Keycode { get; }

        internal int Per { get; }

        internal IntPtr Saved { get; }
    }


    private static void PressMapped(IntPtr display, byte keycode, uint keysym)
    {
        bool shift = Column(display, keycode, keysym) == 1;
        byte shiftCode = 0;
        if (shift)
        {
            shiftCode = X11Interop.KeysymToKeycode(display, 0xffe1);
            if (shiftCode != 0)
            {
                _ = X11Interop.FakeKey(display, shiftCode, 1, 0);
            }
        }

        Tap(display, keycode);
        if (shiftCode != 0)
        {
            _ = X11Interop.FakeKey(display, shiftCode, 0, 0);
        }
    }

    private static void Tap(IntPtr display, byte keycode)
    {
        _ = X11Interop.FakeKey(display, keycode, 1, 0);
        _ = X11Interop.FakeKey(display, keycode, 0, 0);
    }

    private void ReleaseAll(IntPtr display)
    {
        foreach (uint button in _buttonsDown.ToArray())
        {
            _ = X11Interop.FakeButton(display, button, 0, 0);
        }

        foreach (uint key in _keysDown.ToArray())
        {
            _ = X11Interop.FakeKey(display, key, 0, 0);
        }

        _buttonsDown.Clear();
        _keysDown.Clear();
        _ = X11Interop.Sync(display, 0);
    }

    private static int Column(IntPtr display, byte keycode, uint keysym)
    {
        IntPtr map = X11Interop.GetKeyboardMapping(display, keycode, 1, out int per);
        if (map == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            for (int i = 0; i < per; i++)
            {
                if (ReadKeysym(map, i) == keysym)
                {
                    return i;
                }
            }
        }
        finally
        {
            _ = X11Interop.Free(map);
        }

        return 0;
    }

    private static string Identity(string? wmClass, int pid)
    {
        if (pid <= 0)
        {
            return UnknownAppIdentity;
        }

        string? exe;
        try
        {
            exe = File.ResolveLinkTarget("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/exe", returnFinalTarget: false)?.Name;
        }
        catch (IOException)
        {
            exe = null;
        }
        catch (UnauthorizedAccessException)
        {
            exe = null;
        }

        if (string.IsNullOrEmpty(exe))
        {
            return UnknownAppIdentity;
        }

        return string.IsNullOrEmpty(wmClass) ? exe : wmClass + "/" + exe;
    }

    private static string? ReadClass(IntPtr display, IntPtr window, IntPtr atom)
    {
        byte[]? bytes = ReadBytes(display, window, atom);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        int split = Array.IndexOf(bytes, (byte)0);
        string instance = split < 0
            ? System.Text.Encoding.Latin1.GetString(bytes)
            : System.Text.Encoding.Latin1.GetString(bytes, 0, split);
        string? klass = split >= 0 && split + 1 < bytes.Length
            ? System.Text.Encoding.Latin1.GetString(bytes, split + 1, bytes.Length - split - 1).TrimEnd('\0')
            : null;
        return string.IsNullOrEmpty(klass) ? instance : klass;
    }

    private static string? ReadUtf8Property(IntPtr display, IntPtr window, IntPtr atom, IntPtr utf8Type)
    {
        byte[]? bytes = ReadBytes(display, window, atom);
        if (bytes is null)
        {
            return null;
        }

        _ = utf8Type;
        return System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    private static string? ReadLatin1Property(IntPtr display, IntPtr window, IntPtr atom)
    {
        byte[]? bytes = ReadBytes(display, window, atom);
        return bytes is null ? null : System.Text.Encoding.Latin1.GetString(bytes).TrimEnd('\0');
    }

    private static int ReadCardinal(IntPtr display, IntPtr window, IntPtr atom)
    {
        long[]? values = ReadWindowList(display, window, atom);
        return values is null || values.Length == 0 ? 0 : (int)values[0];
    }

    private static long[]? ReadWindowList(IntPtr display, IntPtr window, IntPtr atom)
    {
        if (X11Interop.GetWindowProperty(display, window, atom, 0, 256, 0, IntPtr.Zero, out IntPtr type, out int format, out nuint items, out _, out IntPtr data) != 0
            || type == IntPtr.Zero
            || data == IntPtr.Zero
            || items == 0)
        {
            if (data != IntPtr.Zero)
            {
                _ = X11Interop.Free(data);
            }

            return null;
        }

        try
        {
            int count = (int)items;
            if (format == 8)
            {
                return null;
            }

            var values = new long[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = MarshalReadWindow(data, (uint)i);
            }

            return values;
        }
        finally
        {
            _ = X11Interop.Free(data);
        }
    }

    private static byte[]? ReadBytes(IntPtr display, IntPtr window, IntPtr atom)
    {
        if (X11Interop.GetWindowProperty(display, window, atom, 0, 256, 0, IntPtr.Zero, out IntPtr type, out _, out nuint items, out _, out IntPtr data) != 0
            || type == IntPtr.Zero
            || data == IntPtr.Zero
            || items == 0)
        {
            if (data != IntPtr.Zero)
            {
                _ = X11Interop.Free(data);
            }

            return null;
        }

        try
        {
            var bytes = new byte[(int)items];
            System.Runtime.InteropServices.Marshal.Copy(data, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            _ = X11Interop.Free(data);
        }
    }

    private static RawFrame CopyBgra(X11Interop.ImageView image)
    {
        int width = image.Width;
        int height = image.Height;
        int stride = width * 4;
        var pixels = new byte[stride * height];
        int redShift = Shift(image.RedMask);
        int greenShift = Shift(image.GreenMask);
        int blueShift = Shift(image.BlueMask);
        int bpp = image.BitsPerPixel <= 0 ? 32 : image.BitsPerPixel;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ulong pixel = ReadPixel(image, x, y, bpp);
                int offset = (y * stride) + (x * 4);
                pixels[offset] = (byte)((pixel & image.BlueMask) >> blueShift);
                pixels[offset + 1] = (byte)((pixel & image.GreenMask) >> greenShift);
                pixels[offset + 2] = (byte)((pixel & image.RedMask) >> redShift);
                pixels[offset + 3] = 255;
            }
        }

        return new RawFrame { Pixels = pixels, Width = width, Height = height, Stride = stride };
    }

    private static ulong ReadPixel(X11Interop.ImageView image, int x, int y, int bpp)
    {
        int bytes = Math.Max(1, bpp / 8);
        IntPtr address = image.Data + (y * image.BytesPerLine) + (x * bytes);
        return bytes switch
        {
            >= 4 => (uint)System.Runtime.InteropServices.Marshal.ReadInt32(address),
            3 => (ulong)(System.Runtime.InteropServices.Marshal.ReadByte(address)
                | (System.Runtime.InteropServices.Marshal.ReadByte(address, 1) << 8)
                | (System.Runtime.InteropServices.Marshal.ReadByte(address, 2) << 16)),
            2 => (ushort)System.Runtime.InteropServices.Marshal.ReadInt16(address),
            _ => System.Runtime.InteropServices.Marshal.ReadByte(address),
        };
    }

    private static int Shift(ulong mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        int shift = 0;
        while ((mask & 1) == 0)
        {
            mask >>= 1;
            shift++;
        }

        return shift;
    }

    private static string KeysymName(string name) => name switch
    {
        "Ctrl" or "Control" => "Control_L",
        "Alt" => "Alt_L",
        "Shift" => "Shift_L",
        "Super" or "Win" or "Meta" => "Super_L",
        "Esc" or "Escape" => "Escape",
        "Enter" or "Return" => "Return",
        "Space" => "space",
        "Backspace" => "BackSpace",
        _ => name,
    };

    private static long MarshalReadWindow(IntPtr data, uint index)
    {
        int offset = (int)index * IntPtr.Size;
        return IntPtr.Size == 8
            ? System.Runtime.InteropServices.Marshal.ReadInt64(data, offset)
            : System.Runtime.InteropServices.Marshal.ReadInt32(data, offset);
    }

    private static void WriteKeysym(IntPtr data, int index, uint keysym)
    {
        if (IntPtr.Size == 8)
        {
            System.Runtime.InteropServices.Marshal.WriteInt64(data, index * 8, keysym);
        }
        else
        {
            System.Runtime.InteropServices.Marshal.WriteInt32(data, index * 4, (int)keysym);
        }
    }

    private static uint ReadKeysym(IntPtr data, int index)
    {
        return IntPtr.Size == 8
            ? (uint)System.Runtime.InteropServices.Marshal.ReadInt64(data, index * 8)
            : (uint)System.Runtime.InteropServices.Marshal.ReadInt32(data, index * 4);
    }

    private static IntPtr MarshalAlloc(int bytes) => System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes);

    private static void MarshalFree(IntPtr pointer) => System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);

    private static ComputerPermissionStatus Granted(ComputerPermissionKind kind, string messageId) =>
        new() { Kind = kind, Granted = true, MessageId = messageId };
}
