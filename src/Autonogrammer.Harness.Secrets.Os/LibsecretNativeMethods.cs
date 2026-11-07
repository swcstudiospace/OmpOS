using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// The libsecret, GLib, GObject and GIO entry points the Linux store calls. Every signature uses <see cref="IntPtr"/>, <see cref="int"/>
/// and <see cref="uint"/> only, and only the non-variadic libsecret functions are bound. Each import restricts library probing to the
/// system directories.
/// </summary>
internal static class LibsecretNativeMethods
{
    internal const string SecretLibrary = "libsecret-1.so.0";
    internal const string GlibLibrary = "libglib-2.0.so.0";
    internal const string GobjectLibrary = "libgobject-2.0.so.0";
    internal const string GioLibrary = "libgio-2.0.so.0";

    [DllImport(SecretLibrary, EntryPoint = "secret_password_storev_sync")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int PasswordStore(IntPtr schema, IntPtr attributes, IntPtr collection, IntPtr label, IntPtr password, IntPtr cancellable, out IntPtr error);

    [DllImport(SecretLibrary, EntryPoint = "secret_password_lookupv_sync")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr PasswordLookup(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(SecretLibrary, EntryPoint = "secret_password_clearv_sync")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int PasswordClear(IntPtr schema, IntPtr attributes, IntPtr cancellable, out IntPtr error);

    [DllImport(SecretLibrary, EntryPoint = "secret_password_searchv_sync")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr PasswordSearch(IntPtr schema, IntPtr attributes, int flags, IntPtr cancellable, out IntPtr error);

    [DllImport(SecretLibrary, EntryPoint = "secret_password_free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void PasswordFree(IntPtr password);

    [DllImport(SecretLibrary, EntryPoint = "secret_retrievable_get_attributes")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr RetrievableGetAttributes(IntPtr retrievable);

    [DllImport(SecretLibrary, EntryPoint = "secret_error_get_quark")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern uint SecretErrorQuark();

    [DllImport(GlibLibrary, EntryPoint = "g_hash_table_new")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr HashTableNew(IntPtr hashFunction, IntPtr equalFunction);

    [DllImport(GlibLibrary, EntryPoint = "g_hash_table_insert")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int HashTableInsert(IntPtr table, IntPtr key, IntPtr value);

    [DllImport(GlibLibrary, EntryPoint = "g_hash_table_lookup")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr HashTableLookup(IntPtr table, IntPtr key);

    [DllImport(GlibLibrary, EntryPoint = "g_hash_table_unref")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void HashTableUnref(IntPtr table);

    [DllImport(GlibLibrary, EntryPoint = "g_error_free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void ErrorFree(IntPtr error);

    [DllImport(GlibLibrary, EntryPoint = "g_list_free")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void ListFree(IntPtr list);

    [DllImport(GobjectLibrary, EntryPoint = "g_object_unref")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void ObjectUnref(IntPtr instance);

    [DllImport(GioLibrary, EntryPoint = "g_cancellable_new")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr CancellableNew();

    [DllImport(GioLibrary, EntryPoint = "g_cancellable_cancel")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void CancellableCancel(IntPtr cancellable);

    [DllImport(GioLibrary, EntryPoint = "g_io_error_quark")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern uint IoErrorQuark();

    [DllImport(GioLibrary, EntryPoint = "g_dbus_error_quark")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern uint DBusErrorQuark();
}
