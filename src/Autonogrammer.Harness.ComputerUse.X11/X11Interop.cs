using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.ComputerUse.X11;

/// <summary>
/// System libX11, libXtst and libXrandr entry points. Loaded from the system, never bundled.
/// No unsafe blocks. Layout offsets were measured on LP64; ILP32 uses the matching field order.
/// </summary>
internal static class X11Interop
{
    internal const int BadAccess = 10;

    internal const int KeyPress = 2;

    internal const int ZPixmap = 2;

    internal const int GrabModeAsync = 1;

    internal const int IsUnmapped = 0;

    internal const uint ShiftMask = 1;

    internal const uint LockMask = 1 << 1;

    internal const uint ControlMask = 1 << 2;

    internal const uint Mod1Mask = 1 << 3;

    internal const uint Mod2Mask = 1 << 4;

    internal const uint Mod5Mask = 1 << 7;

    internal const uint Button1Mask = 1 << 8;

    internal const uint Button2Mask = 1 << 9;

    internal const uint Button3Mask = 1 << 10;

    internal const int AllPlanes = -1;

    private const string LibX11 = "libX11.so.6";

    private const string LibXtst = "libXtst.so.6";

    private const string LibXrandr = "libXrandr.so.2";

    private static readonly ErrorHandlerDelegate ErrorHandler = OnError;

    private static int _lastError;

    private static IntPtr _lastErrorDisplay;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ErrorHandlerDelegate(IntPtr display, IntPtr errorEvent);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DestroyImageDelegate(IntPtr image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IOErrorHandlerDelegate(IntPtr display);

    static X11Interop()
    {
        _ = SetErrorHandler(ErrorHandler);
        _ = SetIOErrorHandler(IOErrorHandler);
    }

    internal static void ClearError()
    {
        _lastError = 0;
        _lastErrorDisplay = IntPtr.Zero;
    }

    internal static int LastError(IntPtr display) => _lastErrorDisplay == display ? _lastError : 0;

    internal static IntPtr Open(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return IntPtr.Zero;
        }

        using Utf8Buffer name = new(displayName);
        return OpenDisplay(name.Pointer);
    }

    internal static IntPtr Intern(IntPtr display, string name)
    {
        using Utf8Buffer native = new(name);
        return InternAtom(display, native.Pointer, 0);
    }

    internal static void DestroyImage(IntPtr image)
    {
        if (image == IntPtr.Zero)
        {
            return;
        }

        int destroyOffset = IntPtr.Size == 8 ? 96 : 68;
        IntPtr function = Marshal.ReadIntPtr(image, destroyOffset);
        DestroyImageDelegate destroy = Marshal.GetDelegateForFunctionPointer<DestroyImageDelegate>(function);
        _ = destroy(image);
    }

    internal static ImageView ReadImage(IntPtr image)
    {
        bool wide = IntPtr.Size == 8;
        int dataOffset = 16;
        return new ImageView(
            Marshal.ReadInt32(image, 0),
            Marshal.ReadInt32(image, 4),
            Marshal.ReadIntPtr(image, dataOffset),
            Marshal.ReadInt32(image, wide ? 44 : 40),
            Marshal.ReadInt32(image, wide ? 48 : 44),
            ReadUnsigned(image, wide ? 56 : 48),
            ReadUnsigned(image, wide ? 64 : 52),
            ReadUnsigned(image, wide ? 72 : 56));
    }

    internal static MonitorView ReadMonitor(IntPtr monitor, int index)
    {
        int stride = IntPtr.Size == 8 ? 56 : 44;
        IntPtr item = monitor + (index * stride);
        bool wide = IntPtr.Size == 8;
        return new MonitorView(
            ReadUnsigned(item, 0),
            Marshal.ReadInt32(item, wide ? 8 : 4) != 0,
            Marshal.ReadInt32(item, wide ? 20 : 16),
            Marshal.ReadInt32(item, wide ? 24 : 20),
            Marshal.ReadInt32(item, wide ? 28 : 24),
            Marshal.ReadInt32(item, wide ? 32 : 28));
    }

    internal static int ReadMapState(IntPtr attributes) => Marshal.ReadInt32(attributes, IntPtr.Size == 8 ? 92 : 64);

    internal static void ReadWindowOrigin(IntPtr attributes, out int x, out int y, out int width, out int height)
    {
        x = Marshal.ReadInt32(attributes, 0);
        y = Marshal.ReadInt32(attributes, 4);
        width = Marshal.ReadInt32(attributes, 8);
        height = Marshal.ReadInt32(attributes, 12);
    }

    internal static int ReadEventType(IntPtr eventPointer) => Marshal.ReadInt32(eventPointer, 0);

    internal static uint ReadEventKeycode(IntPtr eventPointer) => (uint)Marshal.ReadInt32(eventPointer, IntPtr.Size == 8 ? 84 : 52);

    internal sealed class ImageView
    {
        internal ImageView(int width, int height, IntPtr data, int bytesPerLine, int bitsPerPixel, ulong redMask, ulong greenMask, ulong blueMask)
        {
            Width = width;
            Height = height;
            Data = data;
            BytesPerLine = bytesPerLine;
            BitsPerPixel = bitsPerPixel;
            RedMask = redMask;
            GreenMask = greenMask;
            BlueMask = blueMask;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal IntPtr Data { get; }

        internal int BytesPerLine { get; }

        internal int BitsPerPixel { get; }

        internal ulong RedMask { get; }

        internal ulong GreenMask { get; }

        internal ulong BlueMask { get; }
    }

    internal sealed class MonitorView
    {
        internal MonitorView(ulong name, bool primary, int x, int y, int width, int height)
        {
            Name = name;
            Primary = primary;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        internal ulong Name { get; }

        internal bool Primary { get; }

        internal int X { get; }

        internal int Y { get; }

        internal int Width { get; }

        internal int Height { get; }
    }

    [DllImport(LibX11, EntryPoint = "XOpenDisplay")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr OpenDisplay(IntPtr name);

    [DllImport(LibX11, EntryPoint = "XCloseDisplay")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int CloseDisplay(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XSync")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int Sync(IntPtr display, int discard);

    [DllImport(LibX11, EntryPoint = "XFlush")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int Flush(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XFree")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int Free(IntPtr data);

    [DllImport(LibX11, EntryPoint = "XDefaultScreen")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int DefaultScreen(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XRootWindow")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr RootWindow(IntPtr display, int screen);

    [DllImport(LibX11, EntryPoint = "XDisplayWidth")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int DisplayWidth(IntPtr display, int screen);

    [DllImport(LibX11, EntryPoint = "XDisplayHeight")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int DisplayHeight(IntPtr display, int screen);

    [DllImport(LibX11, EntryPoint = "XGetImage")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr GetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, nint planeMask, int format);

    [DllImport(LibX11, EntryPoint = "XGetWindowAttributes")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int GetWindowAttributes(IntPtr display, IntPtr window, IntPtr attributes);

    [DllImport(LibX11, EntryPoint = "XTranslateCoordinates")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int TranslateCoordinates(IntPtr display, IntPtr source, IntPtr destination, int sourceX, int sourceY, out int destX, out int destY, out IntPtr child);

    [DllImport(LibX11, EntryPoint = "XQueryTree")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int QueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent, out IntPtr children, out uint count);

    [DllImport(LibX11, EntryPoint = "XQueryPointer")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int QueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootX, out int rootY, out int winX, out int winY, out uint mask);

    [DllImport(LibX11, EntryPoint = "XInternAtom")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr InternAtom(IntPtr display, IntPtr name, int onlyIfExists);

    [DllImport(LibX11, EntryPoint = "XGetWindowProperty")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int GetWindowProperty(
        IntPtr display,
        IntPtr window,
        IntPtr property,
        nint offset,
        nint length,
        int delete,
        IntPtr type,
        out IntPtr actualType,
        out int actualFormat,
        out nuint items,
        out nuint bytesAfter,
        out IntPtr data);

    [DllImport(LibX11, EntryPoint = "XStringToKeysym")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern nuint StringToKeysymRaw(IntPtr name);

    [DllImport(LibX11, EntryPoint = "XKeysymToKeycode")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern byte KeysymToKeycode(IntPtr display, nuint keysym);

    [DllImport(LibX11, EntryPoint = "XGetKeyboardMapping")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr GetKeyboardMapping(IntPtr display, byte firstKeycode, int keycodeCount, out int keysymsPerKeycode);

    [DllImport(LibX11, EntryPoint = "XChangeKeyboardMapping")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ChangeKeyboardMapping(IntPtr display, int firstKeycode, int keysymsPerKeycode, IntPtr keysyms, int numCodes);

    [DllImport(LibX11, EntryPoint = "XGrabKey")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int GrabKey(IntPtr display, int keycode, uint modifiers, IntPtr window, int ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(LibX11, EntryPoint = "XPending")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int Pending(IntPtr display);

    [DllImport(LibX11, EntryPoint = "XNextEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int NextEvent(IntPtr display, IntPtr eventReturn);

    [DllImport(LibX11, EntryPoint = "XSetErrorHandler")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr SetErrorHandler(ErrorHandlerDelegate handler);

    [DllImport(LibX11, EntryPoint = "XSetIOErrorHandler")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern IntPtr SetIOErrorHandler(IOErrorHandlerDelegate handler);

    [DllImport(LibXtst, EntryPoint = "XTestQueryExtension")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int TestQueryExtension(IntPtr display, out int eventBase, out int errorBase, out int major, out int minor);

    [DllImport(LibXtst, EntryPoint = "XTestFakeMotionEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int FakeMotion(IntPtr display, int screen, int x, int y, nuint delay);

    [DllImport(LibXtst, EntryPoint = "XTestFakeButtonEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int FakeButton(IntPtr display, uint button, int isPress, nuint delay);

    [DllImport(LibXtst, EntryPoint = "XTestFakeKeyEvent")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int FakeKey(IntPtr display, uint keycode, int isPress, nuint delay);

    [DllImport(LibXrandr, EntryPoint = "XRRGetMonitors")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr GetMonitors(IntPtr display, IntPtr window, int getActive, out int count);

    [DllImport(LibXrandr, EntryPoint = "XRRFreeMonitors")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void FreeMonitors(IntPtr monitors);

    internal static bool QueryTest(IntPtr display)
    {
        return TestQueryExtension(display, out _, out _, out _, out _) != 0;
    }

    internal static nuint StringToKeysym(string name)
    {
        using Utf8Buffer native = new(name);
        return StringToKeysymRaw(native.Pointer);
    }

    private static int OnError(IntPtr display, IntPtr errorEvent)
    {
        int codeOffset = IntPtr.Size == 8 ? 32 : 16;
        _lastErrorDisplay = display;
        _lastError = Marshal.ReadByte(errorEvent, codeOffset);
        return 0;
    }

    private static readonly IOErrorHandlerDelegate IOErrorHandler = OnIOError;

    private static int OnIOError(IntPtr display)
    {
        _ = display;
        Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    private static ulong ReadUnsigned(IntPtr address, int offset)
    {
        return IntPtr.Size == 8
            ? (ulong)Marshal.ReadInt64(address, offset)
            : (uint)Marshal.ReadInt32(address, offset);
    }

    private sealed class Utf8Buffer : IDisposable
    {
        internal Utf8Buffer(string text)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text + "\0");
            Pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Pointer, bytes.Length);
        }

        internal IntPtr Pointer { get; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
            }
        }
    }
}
