using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.ComputerUse.Windows;

/// <summary>
/// user32, gdi32, dwmapi, plus kernel32 and advapi32 for the UIPI token check. Entry points resolve at run time.
/// Callers must not invoke them unless <see cref="OperatingSystem.IsWindows"/> is true.
/// </summary>
internal static class WindowsInterop
{
    internal const uint ProcessQueryLimitedInformation = 0x1000;

    internal const uint TokenElevation = 20;

    internal const int DesktopReadObjects = 0x0001;

    internal const uint SrcCopy = 0x00CC0020;

    internal const uint CaptureBlt = 0x40000000;

    internal const int DibRgbColors = 0;

    internal const int BiRgb = 0;

    internal const int DwmwaExtendedFrameBounds = 9;

    internal const int LogPixelsX = 88;

    internal const int CwUseDefault = unchecked((int)0x80000000);

    internal const uint CfUnicodeText = 13;

    internal const uint GmemMoveable = 0x0002;

    internal const uint ModAlt = 0x0001;

    internal const uint ModControl = 0x0002;

    internal const uint ModShift = 0x0004;

    internal const uint ModWin = 0x0008;

    internal const uint WmHotkey = 0x0312;

    internal const uint WmClose = 0x0010;

    internal static readonly IntPtr HwndMessage = new(-3);

    internal static readonly IntPtr PerMonitorV2 = new(-4);

    [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint SendInput(uint count, InputNative[] inputs, int size);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetCursorPos(out Point point);

    [DllImport("user32.dll", EntryPoint = "GetAsyncKeyState")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", EntryPoint = "OpenInputDesktop", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr OpenInputDesktop(uint flags, int inherit, uint desiredAccess);

    [DllImport("user32.dll", EntryPoint = "CloseDesktop", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int CloseDesktop(IntPtr desktop);

    [DllImport("kernel32.dll", EntryPoint = "OpenProcess", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr OpenProcess(uint access, int inherit, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetTokenInformation(
        IntPtr token,
        uint tokenClass,
        out TokenElevationInfo elevation,
        uint length,
        out uint returned);

    [DllImport("user32.dll", EntryPoint = "GetDC")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetDc(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int ReleaseDc(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr CreateCompatibleDc(IntPtr dc);

    [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

    [DllImport("gdi32.dll", EntryPoint = "BitBlt")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int srcX, int srcY, uint rop);

    [DllImport("gdi32.dll", EntryPoint = "GetDIBits")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetDiBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfo info, uint usage);

    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int DeleteDc(IntPtr dc);

    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll", EntryPoint = "GetDeviceCaps")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetDeviceCaps(IntPtr dc, int index);

    [DllImport("user32.dll", EntryPoint = "EnumWindows")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int EnumWindows(EnumWindowsProc callback, IntPtr param);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetWindowText(IntPtr window, char[] text, int length);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetClassName(IntPtr window, char[] name, int length);

    [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int IsIconic(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect rect, int size);

    [DllImport("user32.dll", EntryPoint = "EnumDisplayMonitors")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnumProc callback, IntPtr param);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr CreateWindowEx(
        uint exStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", EntryPoint = "DestroyWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int DestroyWindow(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetMessageW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);

    [DllImport("user32.dll", EntryPoint = "TranslateMessage")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int TranslateMessage(ref Message message);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    [DllImport("user32.dll", EntryPoint = "OpenClipboard")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int OpenClipboard(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "CloseClipboard")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int CloseClipboard();

    [DllImport("user32.dll", EntryPoint = "EmptyClipboard")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int EmptyClipboard();

    [DllImport("user32.dll", EntryPoint = "GetClipboardData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", EntryPoint = "SetClipboardData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", EntryPoint = "IsClipboardFormatAvailable")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll", EntryPoint = "GlobalAlloc")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", EntryPoint = "GlobalLock")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", EntryPoint = "GlobalUnlock")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern int GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", EntryPoint = "GlobalSize")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern nuint GlobalSize(IntPtr memory);

    internal delegate int EnumWindowsProc(IntPtr window, IntPtr param);

    internal delegate int MonitorEnumProc(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr param);

    /// <summary>LP64 INPUT. Type at 0, union at 8. Mouse and key fields share the union.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct InputNative
    {
        [FieldOffset(0)]
        internal uint Type;

        [FieldOffset(8)]
        internal int Dx;

        [FieldOffset(12)]
        internal int Dy;

        [FieldOffset(16)]
        internal uint MouseData;

        [FieldOffset(20)]
        internal uint Flags;

        [FieldOffset(24)]
        internal uint Time;

        [FieldOffset(32)]
        internal nuint Extra;

        [FieldOffset(8)]
        internal ushort VirtualKey;

        [FieldOffset(10)]
        internal ushort Scan;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;

        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;

        internal int Top;

        internal int Right;

        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenElevationInfo
    {
        internal uint TokenIsElevated;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        internal uint Size;

        internal int Width;

        internal int Height;

        internal ushort Planes;

        internal ushort BitCount;

        internal uint Compression;

        internal uint SizeImage;

        internal int XPelsPerMeter;

        internal int YPelsPerMeter;

        internal uint ClrUsed;

        internal uint ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfo
    {
        internal BitmapInfoHeader Header;

        internal uint Colors;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal int Size;

        internal Rect Monitor;

        internal Rect Work;

        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal IntPtr Window;

        internal uint Id;

        internal IntPtr WParam;

        internal IntPtr LParam;

        internal uint Time;

        internal Point Point;
    }
}
