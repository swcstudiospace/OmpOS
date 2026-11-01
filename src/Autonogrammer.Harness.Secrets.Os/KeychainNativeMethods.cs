using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The CoreFoundation and Security framework entry points the macOS store calls, by absolute framework path. Every signature uses
/// <see cref="IntPtr"/>, <see cref="int"/>, <see cref="uint"/> and <see cref="byte"/> only (<c>CFIndex</c> is pointer-sized, <c>Boolean</c> is one byte).
/// Each import restricts library probing to the system directories. This code is compiled on every platform and runs only on macOS.
/// </summary>
internal static class KeychainNativeMethods
{
    internal const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    internal const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFRelease")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void Release(IntPtr instance);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFStringCreateWithBytes")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr StringCreateWithBytes(IntPtr allocator, IntPtr bytes, IntPtr byteCount, uint encoding, byte isExternalRepresentation);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFStringGetMaximumSizeForEncoding")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr StringGetMaximumSizeForEncoding(IntPtr length, uint encoding);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFStringGetLength")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr StringGetLength(IntPtr text);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFStringGetCString")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern byte StringGetCString(IntPtr text, IntPtr buffer, IntPtr bufferSize, uint encoding);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDataCreateMutable")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DataCreateMutable(IntPtr allocator, IntPtr capacity);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDataAppendBytes")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void DataAppendBytes(IntPtr data, IntPtr bytes, IntPtr length);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDataGetMutableBytePtr")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DataGetMutableBytePtr(IntPtr data);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDataGetBytePtr")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DataGetBytePtr(IntPtr data);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDataGetLength")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DataGetLength(IntPtr data);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDictionaryCreateMutable")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DictionaryCreateMutable(IntPtr allocator, IntPtr capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDictionarySetValue")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void DictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFDictionaryGetValue")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr DictionaryGetValue(IntPtr dictionary, IntPtr key);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFArrayGetCount")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr ArrayGetCount(IntPtr array);

    [DllImport(CoreFoundationLibrary, EntryPoint = "CFArrayGetValueAtIndex")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr ArrayGetValueAtIndex(IntPtr array, IntPtr index);

    [DllImport(SecurityLibrary, EntryPoint = "SecItemAdd")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ItemAdd(IntPtr attributes, IntPtr result);

    [DllImport(SecurityLibrary, EntryPoint = "SecItemCopyMatching")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport(SecurityLibrary, EntryPoint = "SecItemUpdate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [DllImport(SecurityLibrary, EntryPoint = "SecItemDelete")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int ItemDelete(IntPtr query);
}
