using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.ComputerUse.MacOS;

/// <summary>
/// ApplicationServices, CoreFoundation and Carbon entry points, loaded by absolute framework path.
/// Callers must not invoke them unless <see cref="OperatingSystem.IsMacOS"/> is true.
/// </summary>
internal static class MacOSInterop
{
    internal const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    internal const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    internal const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    internal const uint HidEventTap = 0;

    internal const uint MouseMoved = 5;

    internal const uint LeftMouseDown = 1;

    internal const uint LeftMouseUp = 2;

    internal const uint RightMouseDown = 3;

    internal const uint RightMouseUp = 4;

    internal const uint OtherMouseDown = 25;

    internal const uint OtherMouseUp = 26;

    internal const uint ScrollEventUnitLine = 1;

    internal const uint EventSourceHid = 1;

    internal const uint WindowListOnScreen = 1;

    internal const int StringEncodingUtf8 = 0x08000100;

    internal const uint CmdKey = 1 << 8;

    internal const uint ShiftKey = 1 << 9;

    internal const uint OptionKey = 1 << 11;

    internal const uint ControlKey = 1 << 12;

    internal const uint VkEscape = 0x35;

    [DllImport(ApplicationServices, EntryPoint = "CGEventCreateMouseEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr CreateMouseEvent(IntPtr source, uint type, CgPoint point, uint button);

    [DllImport(ApplicationServices, EntryPoint = "CGEventCreateKeyboardEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr CreateKeyboardEvent(IntPtr source, ushort virtualKey, [MarshalAs(UnmanagedType.I1)] bool down);

    [DllImport(ApplicationServices, EntryPoint = "CGEventKeyboardSetUnicodeString")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void KeyboardSetUnicodeString(IntPtr evt, nuint length, IntPtr text);

    [DllImport(ApplicationServices, EntryPoint = "CGEventCreateScrollWheelEvent2")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr CreateScrollWheelEvent(IntPtr source, uint units, uint wheelCount, int wheel1);

    [DllImport(ApplicationServices, EntryPoint = "CGEventPost")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void Post(uint tap, IntPtr evt);

    [DllImport(ApplicationServices, EntryPoint = "CGEventSourceButtonState")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ButtonState(uint state, uint button);

    [DllImport(ApplicationServices, EntryPoint = "CGEventCreate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr CreateEvent(IntPtr source);

    [DllImport(ApplicationServices, EntryPoint = "CGEventGetLocation")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CgPoint GetLocation(IntPtr evt);

    [DllImport(ApplicationServices, EntryPoint = "CGGetActiveDisplayList")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int GetActiveDisplayList(uint max, uint[] displays, out uint count);

    [DllImport(ApplicationServices, EntryPoint = "CGDisplayBounds")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CgRect DisplayBounds(uint display);

    [DllImport(ApplicationServices, EntryPoint = "CGDisplayPixelsWide")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint DisplayPixelsWide(uint display);

    [DllImport(ApplicationServices, EntryPoint = "CGDisplayPixelsHigh")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nuint DisplayPixelsHigh(uint display);

    [DllImport(ApplicationServices, EntryPoint = "CGMainDisplayID")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern uint MainDisplayId();

    [DllImport(ApplicationServices, EntryPoint = "CGWindowListCopyWindowInfo")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr WindowListCopyWindowInfo(uint option, uint relativeTo);

    [DllImport(ApplicationServices, EntryPoint = "CGPreflightScreenCaptureAccess")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PreflightScreenCaptureAccess();

    [DllImport(ApplicationServices, EntryPoint = "CGRequestScreenCaptureAccess")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool RequestScreenCaptureAccess();

    [DllImport(ApplicationServices, EntryPoint = "AXIsProcessTrusted")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool IsProcessTrusted();

    [DllImport(CoreFoundation, EntryPoint = "CFRelease")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void Release(IntPtr value);

    [DllImport(CoreFoundation, EntryPoint = "CFArrayGetCount")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern nint ArrayCount(IntPtr array);

    [DllImport(CoreFoundation, EntryPoint = "CFArrayGetValueAtIndex")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr ArrayValue(IntPtr array, nint index);

    [DllImport(CoreFoundation, EntryPoint = "CFDictionaryGetValue")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DictionaryValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundation, EntryPoint = "CFStringCreateWithCString")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr StringCreate(IntPtr allocator, byte[] text, int encoding);

    [DllImport(CoreFoundation, EntryPoint = "CFStringGetCString")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool StringGet(IntPtr text, byte[] buffer, nint length, int encoding);

    [DllImport(CoreFoundation, EntryPoint = "CFNumberGetValue")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool NumberGet(IntPtr number, int type, out int value);

    [DllImport(Carbon, EntryPoint = "RegisterEventHotKey")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int RegisterEventHotKey(uint keyCode, uint modifiers, HotKeyId id, IntPtr target, uint options, out IntPtr hotKey);

    [DllImport(Carbon, EntryPoint = "UnregisterEventHotKey")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int UnregisterEventHotKey(IntPtr hotKey);

    [DllImport(Carbon, EntryPoint = "GetEventDispatcherTarget")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr GetEventDispatcherTarget();

    [DllImport(Carbon, EntryPoint = "InstallEventHandler")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int InstallEventHandler(IntPtr target, EventHandler handler, uint count, IntPtr spec, IntPtr user, out IntPtr installed);

    [DllImport(Carbon, EntryPoint = "RemoveEventHandler")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int RemoveEventHandler(IntPtr handler);

    [DllImport(Carbon, EntryPoint = "ReceiveNextEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ReceiveNextEvent(uint count, IntPtr spec, double timeout, [MarshalAs(UnmanagedType.I1)] bool pull, out IntPtr evt);

    internal delegate int EventHandler(IntPtr handler, IntPtr evt, IntPtr user);

    [StructLayout(LayoutKind.Sequential)]
    internal struct CgPoint
    {
        internal double X;

        internal double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CgRect
    {
        internal CgPoint Origin;

        internal CgSize Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CgSize
    {
        internal double Width;

        internal double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HotKeyId
    {
        internal uint Signature;

        internal uint Id;
    }
}
