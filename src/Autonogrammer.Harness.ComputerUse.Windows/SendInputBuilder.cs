using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.ComputerUse.Windows;

/// <summary>
/// Pure SendInput records. No P/Invoke. Absolute motion is <c>MOUSEEVENTF_ABSOLUTE</c> plus
/// <c>MOUSEEVENTF_VIRTUALDESK</c>, normalised 0 to 65535 over the virtual desktop.
/// </summary>
public static class SendInputBuilder
{
    /// <summary>Normalised origin.</summary>
    public const int AbsoluteOrigin = 0;

    /// <summary>Normalised far corner.</summary>
    public const int AbsoluteFarCorner = 65535;

    /// <summary><c>INPUT_MOUSE</c>.</summary>
    public const uint InputMouse = 0;

    /// <summary><c>INPUT_KEYBOARD</c>.</summary>
    public const uint InputKeyboard = 1;

    /// <summary><c>MOUSEEVENTF_ABSOLUTE</c>.</summary>
    public const uint MouseEventFAbsolute = 0x8000;

    /// <summary><c>MOUSEEVENTF_VIRTUALDESK</c>.</summary>
    public const uint MouseEventFVirtualDesk = 0x4000;

    /// <summary><c>MOUSEEVENTF_MOVE</c>.</summary>
    public const uint MouseEventFMove = 0x0001;

    /// <summary><c>MOUSEEVENTF_LEFTDOWN</c>.</summary>
    public const uint MouseEventFLeftDown = 0x0002;

    /// <summary><c>MOUSEEVENTF_LEFTUP</c>.</summary>
    public const uint MouseEventFLeftUp = 0x0004;

    /// <summary><c>MOUSEEVENTF_RIGHTDOWN</c>.</summary>
    public const uint MouseEventFRightDown = 0x0008;

    /// <summary><c>MOUSEEVENTF_RIGHTUP</c>.</summary>
    public const uint MouseEventFRightUp = 0x0010;

    /// <summary><c>MOUSEEVENTF_MIDDLEDOWN</c>.</summary>
    public const uint MouseEventFMiddleDown = 0x0020;

    /// <summary><c>MOUSEEVENTF_MIDDLEUP</c>.</summary>
    public const uint MouseEventFMiddleUp = 0x0040;

    /// <summary><c>MOUSEEVENTF_WHEEL</c>.</summary>
    public const uint MouseEventFWheel = 0x0800;

    /// <summary><c>MOUSEEVENTF_HWHEEL</c>.</summary>
    public const uint MouseEventFHwheel = 0x1000;

    /// <summary><c>KEYEVENTF_KEYUP</c>.</summary>
    public const uint KeyEventFKeyUp = 0x0002;

    /// <summary><c>KEYEVENTF_UNICODE</c>.</summary>
    public const uint KeyEventFUnicode = 0x0004;

    /// <summary><c>KEYEVENTF_SCANCODE</c>.</summary>
    public const uint KeyEventFScanCode = 0x0008;

    /// <summary>Flags for an absolute virtual-desktop move.</summary>
    public const uint AbsoluteMoveFlags = MouseEventFMove | MouseEventFAbsolute | MouseEventFVirtualDesk;

    /// <summary>
    /// Maps a virtual-desktop pixel to the 0..65535 absolute range. The origin is 0 and the far corner
    /// (origin + extent - 1) is 65535. Out-of-range points clamp. They do not wrap.
    /// </summary>
    /// <param name="x">Virtual-desktop X.</param>
    /// <param name="y">Virtual-desktop Y.</param>
    /// <param name="originX">Virtual-desktop left, which may be negative.</param>
    /// <param name="originY">Virtual-desktop top, which may be negative.</param>
    /// <param name="width">Virtual-desktop width in pixels. Must be positive.</param>
    /// <param name="height">Virtual-desktop height in pixels. Must be positive.</param>
    /// <returns>The normalised point.</returns>
    internal static AbsolutePoint NormalizeAbsolute(int x, int y, int originX, int originY, int width, int height)
    {
        return new AbsolutePoint(ScaleAxis(x, originX, width), ScaleAxis(y, originY, height));
    }

    /// <summary>
    /// Scales a per-monitor logical point by <paramref name="dpiFactor"/> (1 is 96 DPI, 1.5 is 144 DPI)
    /// and then runs the virtual-desktop normaliser. Scale is applied once, with
    /// <see cref="MidpointRounding.ToEven"/>.
    /// </summary>
    /// <param name="x">Logical X on the monitor.</param>
    /// <param name="y">Logical Y on the monitor.</param>
    /// <param name="dpiFactor">Fractional pixels-per-logical-pixel factor. Must be positive.</param>
    /// <param name="originX">Virtual-desktop left in physical pixels.</param>
    /// <param name="originY">Virtual-desktop top in physical pixels.</param>
    /// <param name="width">Virtual-desktop width in physical pixels.</param>
    /// <param name="height">Virtual-desktop height in physical pixels.</param>
    /// <returns>The normalised point.</returns>
    internal static AbsolutePoint NormalizeAfterDpi(
        int x,
        int y,
        double dpiFactor,
        int originX,
        int originY,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiFactor);
        int physicalX = (int)Math.Round(x * dpiFactor, MidpointRounding.ToEven);
        int physicalY = (int)Math.Round(y * dpiFactor, MidpointRounding.ToEven);
        return NormalizeAbsolute(physicalX, physicalY, originX, originY, width, height);
    }

    /// <summary>One absolute move. Does not call SendInput.</summary>
    /// <param name="x">Virtual-desktop X.</param>
    /// <param name="y">Virtual-desktop Y.</param>
    /// <param name="originX">Virtual-desktop left.</param>
    /// <param name="originY">Virtual-desktop top.</param>
    /// <param name="width">Virtual-desktop width.</param>
    /// <param name="height">Virtual-desktop height.</param>
    /// <returns>The mouse INPUT record.</returns>
    internal static InputRecord MoveAbsolute(int x, int y, int originX, int originY, int width, int height)
    {
        AbsolutePoint point = NormalizeAbsolute(x, y, originX, originY, width, height);
        return new InputRecord
        {
            Type = InputMouse,
            Dx = point.X,
            Dy = point.Y,
            Flags = AbsoluteMoveFlags,
        };
    }

    /// <summary>Button down or up. Buttons are 1, 2 or 3.</summary>
    /// <param name="button">Button 1, 2 or 3.</param>
    /// <param name="down"><see langword="true"/> for down.</param>
    /// <returns>The mouse INPUT record.</returns>
    internal static InputRecord PointerButton(int button, bool down)
    {
        uint flags = (button, down) switch
        {
            (1, true) => MouseEventFLeftDown,
            (1, false) => MouseEventFLeftUp,
            (2, true) => MouseEventFMiddleDown,
            (2, false) => MouseEventFMiddleUp,
            (3, true) => MouseEventFRightDown,
            (3, false) => MouseEventFRightUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };
        return new InputRecord { Type = InputMouse, Flags = flags };
    }

    /// <summary>One scroll click. Buttons are 4, 5, 6 or 7.</summary>
    /// <param name="button">Scroll button.</param>
    /// <returns>The mouse INPUT record.</returns>
    internal static InputRecord ScrollClick(int button)
    {
        (uint flags, uint data) = button switch
        {
            4 => (MouseEventFWheel, 120u),
            5 => (MouseEventFWheel, unchecked((uint)-120)),
            6 => (MouseEventFHwheel, unchecked((uint)-120)),
            7 => (MouseEventFHwheel, 120u),
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        };
        return new InputRecord { Type = InputMouse, Flags = flags, MouseData = data };
    }

    /// <summary>Encodes text as <c>KEYEVENTF_UNICODE</c> down/up pairs. One pair per UTF-16 code unit.</summary>
    /// <param name="text">Characters to inject. Not copied into any failure message.</param>
    /// <returns>The keyboard INPUT records.</returns>
    internal static InputRecord[] EncodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var records = new InputRecord[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            ushort unit = text[i];
            records[i * 2] = Unicode(unit, up: false);
            records[(i * 2) + 1] = Unicode(unit, up: true);
        }

        return records;
    }

    /// <summary>
    /// Encodes a chord as scan-code key down/up pairs in order. Index 0 is the first key down, index 1 its up.
    /// </summary>
    /// <param name="scanCodes">Scan codes in chord order.</param>
    /// <returns>The keyboard INPUT records.</returns>
    internal static InputRecord[] EncodeChord(ReadOnlySpan<ushort> scanCodes)
    {
        var records = new InputRecord[scanCodes.Length * 2];
        for (int i = 0; i < scanCodes.Length; i++)
        {
            records[i * 2] = Scan(scanCodes[i], up: false);
            records[(i * 2) + 1] = Scan(scanCodes[i], up: true);
        }

        return records;
    }

    /// <summary>Scan-code down or up for one host key token. Unknown names throw; the message has no typed text.</summary>
    /// <param name="name">Host key token such as <c>Escape</c>.</param>
    /// <param name="down"><see langword="true"/> for down.</param>
    /// <returns>The keyboard INPUT record.</returns>
    internal static InputRecord KeyScan(string name, bool down)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!TryScan(name, out ushort scan))
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Computer Use key token is unknown.");
        }

        return Scan(scan, !down);
    }

    private static InputRecord Unicode(ushort unit, bool up)
    {
        return new InputRecord
        {
            Type = InputKeyboard,
            Scan = unit,
            Flags = up ? KeyEventFUnicode | KeyEventFKeyUp : KeyEventFUnicode,
        };
    }

    private static InputRecord Scan(ushort scan, bool up)
    {
        return new InputRecord
        {
            Type = InputKeyboard,
            Scan = scan,
            Flags = up ? KeyEventFScanCode | KeyEventFKeyUp : KeyEventFScanCode,
        };
    }

    private static int ScaleAxis(int value, int origin, int extent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        long delta = (long)value - origin;
        if (delta <= 0)
        {
            return AbsoluteOrigin;
        }

        if (delta >= extent - 1)
        {
            return AbsoluteFarCorner;
        }

        return (int)(delta * AbsoluteFarCorner / (extent - 1));
    }

    private static bool TryScan(string name, out ushort scan)
    {
        switch (name)
        {
            case "Escape":
                scan = 0x01;
                return true;
            case "A":
                scan = 0x1E;
                return true;
            case "Control":
                scan = 0x1D;
                return true;
            case "Alt":
                scan = 0x38;
                return true;
            case "Shift":
                scan = 0x2A;
                return true;
            case "LWin":
                scan = 0x5B;
                return true;
            default:
                scan = 0;
                return false;
        }
    }

    /// <summary>Normalised absolute coordinates. 0 is the virtual-desktop origin. 65535 is the far corner.</summary>
    /// <param name="X">Normalised X.</param>
    /// <param name="Y">Normalised Y.</param>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct AbsolutePoint(int X, int Y);

    /// <summary>One INPUT record. The backend copies this into the native union only on Windows.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct InputRecord
    {
        /// <summary><see cref="InputMouse"/> or <see cref="InputKeyboard"/>.</summary>
        public uint Type { get; init; }

        /// <summary>Absolute or relative X. Unused for keyboard records.</summary>
        public int Dx { get; init; }

        /// <summary>Absolute or relative Y. Unused for keyboard records.</summary>
        public int Dy { get; init; }

        /// <summary>Mouse or key flags.</summary>
        public uint Flags { get; init; }

        /// <summary>Wheel delta, or zero.</summary>
        public uint MouseData { get; init; }

        /// <summary>Scan code, or the UTF-16 unit when <see cref="KeyEventFUnicode"/> is set.</summary>
        public ushort Scan { get; init; }
    }
}
