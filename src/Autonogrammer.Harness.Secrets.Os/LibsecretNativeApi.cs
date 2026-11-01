using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="ILibsecretApi"/> over libsecret 0.21.4 (<c>libsecret-1.so.0</c>). Every library is loaded with
/// <see cref="NativeLibrary.TryLoad(string, Assembly, DllImportSearchPath?, out IntPtr)"/> first, so a missing library is a
/// <see cref="DllNotFoundException"/> and never a crash. Strings are marshalled by hand into native memory that is zeroed after the call.
/// </summary>
internal sealed class LibsecretNativeApi : ILibsecretApi
{
    private const string DefaultCollection = "default";
    private const int SearchAll = 2;
    private const int SearchUnlock = 4;
    private const int GioErrorCancelled = 19;
    private const int SchemaAttributeCapacity = 32;
    private const int SchemaReservedPointers = 7;

    private static readonly Lock Gate = new();
    private static NativeSymbols? s_symbols;

    private readonly string _app;

    /// <summary>Creates the binding for the item attribute <c>app = autonogrammer</c>.</summary>
    public LibsecretNativeApi()
        : this(LibsecretAttributes.AppValue)
    {
    }

    /// <summary>Creates the binding for another <c>app</c> attribute value, so the live test works on items that are not the application's own.</summary>
    /// <param name="app">The value of the <c>app</c> attribute.</param>
    internal LibsecretNativeApi(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        _app = app;
    }

    /// <inheritdoc />
    public void Store(string name, SecretString secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeSymbols symbols = Load();
        using var schema = new SchemaBlock();
        using var attributes = new AttributeTable(symbols, LibsecretAttributes.For(name, _app));
        using NativeUtf8 collection = NativeUtf8.Create(DefaultCollection, nameof(name));
        using NativeUtf8 label = NativeUtf8.Create("Autonogrammer: " + name, nameof(name));
        using NativeUtf8 password = NativeUtf8.Create(secret.Reveal(), nameof(secret));
        using var cancellable = new CancellableScope(cancellationToken);

        int stored = LibsecretNativeMethods.PasswordStore(schema.Pointer, attributes.Pointer, collection.Pointer, label.Pointer, password.Pointer, cancellable.Handle, out IntPtr error);
        ThrowIfError(error, cancellationToken);
        if (stored == 0)
        {
            throw LinuxLibsecretStore.Failed(LibsecretErrorDomain.Other, 0);
        }
    }

    /// <inheritdoc />
    public SecretString? Lookup(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeSymbols symbols = Load();
        using var schema = new SchemaBlock();
        using var attributes = new AttributeTable(symbols, LibsecretAttributes.For(name, _app));
        using var cancellable = new CancellableScope(cancellationToken);

        IntPtr found = LibsecretNativeMethods.PasswordLookup(schema.Pointer, attributes.Pointer, cancellable.Handle, out IntPtr error);
        ThrowIfError(error, cancellationToken);
        if (found == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            string? value = NativeUtf8.Read(found);
            return string.IsNullOrEmpty(value) ? null : new SecretString(value);
        }
        finally
        {
            LibsecretNativeMethods.PasswordFree(found);
        }
    }

    /// <inheritdoc />
    public bool Clear(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeSymbols symbols = Load();
        using var schema = new SchemaBlock();
        using var attributes = new AttributeTable(symbols, LibsecretAttributes.For(name, _app));
        using var cancellable = new CancellableScope(cancellationToken);

        int removed = LibsecretNativeMethods.PasswordClear(schema.Pointer, attributes.Pointer, cancellable.Handle, out IntPtr error);
        ThrowIfError(error, cancellationToken);
        return removed != 0;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Search(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeSymbols symbols = Load();
        using var schema = new SchemaBlock();
        using var attributes = new AttributeTable(symbols, LibsecretAttributes.ForAll(_app));
        using NativeUtf8 nameKey = NativeUtf8.Create(LibsecretAttributes.NameKey, nameof(nameKey));
        using var cancellable = new CancellableScope(cancellationToken);

        IntPtr list = LibsecretNativeMethods.PasswordSearch(schema.Pointer, attributes.Pointer, SearchAll | SearchUnlock, cancellable.Handle, out IntPtr error);
        ThrowIfError(error, cancellationToken);

        List<string> names = [];
        try
        {
            // GList: { gpointer data; GList *next; GList *prev; }. Each data is a SecretRetrievable that this call owns.
            for (IntPtr node = list; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
            {
                IntPtr retrievable = Marshal.ReadIntPtr(node, 0);
                IntPtr itemAttributes = LibsecretNativeMethods.RetrievableGetAttributes(retrievable);
                if (itemAttributes == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    if (NativeUtf8.Read(LibsecretNativeMethods.HashTableLookup(itemAttributes, nameKey.Pointer)) is { Length: > 0 } itemName)
                    {
                        names.Add(itemName);
                    }
                }
                finally
                {
                    LibsecretNativeMethods.HashTableUnref(itemAttributes);
                }
            }
        }
        finally
        {
            for (IntPtr node = list; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
            {
                LibsecretNativeMethods.ObjectUnref(Marshal.ReadIntPtr(node, 0));
            }

            LibsecretNativeMethods.ListFree(list);
        }

        return names;
    }

    private static NativeSymbols Load()
    {
        lock (Gate)
        {
            if (s_symbols is not null)
            {
                return s_symbols;
            }

            Assembly assembly = typeof(LibsecretNativeApi).Assembly;
            IntPtr glib = IntPtr.Zero;
            foreach (string library in (string[])[LibsecretNativeMethods.SecretLibrary, LibsecretNativeMethods.GlibLibrary, LibsecretNativeMethods.GobjectLibrary, LibsecretNativeMethods.GioLibrary])
            {
                if (!NativeLibrary.TryLoad(library, assembly, DllImportSearchPath.SafeDirectories, out IntPtr handle))
                {
                    throw new DllNotFoundException($"The library {library} could not be loaded.");
                }

                if (string.Equals(library, LibsecretNativeMethods.GlibLibrary, StringComparison.Ordinal))
                {
                    glib = handle;
                }
            }

            s_symbols = new NativeSymbols(NativeLibrary.GetExport(glib, "g_str_hash"), NativeLibrary.GetExport(glib, "g_str_equal"));
            return s_symbols;
        }
    }

    private static void ThrowIfError(IntPtr error, CancellationToken cancellationToken)
    {
        if (error == IntPtr.Zero)
        {
            return;
        }

        // GError: { GQuark domain (guint32); gint code; gchar *message }. Only the domain and code are kept; the free-text message is dropped.
        uint domainQuark = (uint)Marshal.ReadInt32(error, 0);
        int code = Marshal.ReadInt32(error, sizeof(int));
        LibsecretNativeMethods.ErrorFree(error);

        LibsecretErrorDomain domain = LibsecretErrorDomain.Other;
        if (domainQuark == LibsecretNativeMethods.SecretErrorQuark())
        {
            domain = LibsecretErrorDomain.Secret;
        }
        else if (domainQuark == LibsecretNativeMethods.IoErrorQuark())
        {
            domain = LibsecretErrorDomain.Gio;
        }
        else if (domainQuark == LibsecretNativeMethods.DBusErrorQuark())
        {
            domain = LibsecretErrorDomain.DBus;
        }

        if (domain == LibsecretErrorDomain.Gio && code == GioErrorCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        throw LinuxLibsecretStore.Failed(domain, code);
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private sealed record NativeSymbols(IntPtr HashFunction, IntPtr EqualFunction);

    /// <summary>A hand-built <c>SecretSchema</c> with the string attributes <c>app</c> and <c>name</c>, in zeroed unmanaged memory.</summary>
    private sealed class SchemaBlock : IDisposable
    {
        private readonly List<NativeUtf8> _strings = [];
        private IntPtr _memory;

        public SchemaBlock()
        {
            // struct SecretSchema { const gchar *name; SecretSchemaFlags flags; SecretSchemaAttribute attributes[32]; gint reserved; gpointer reserved1..7; }
            // struct SecretSchemaAttribute { const gchar *name; SecretSchemaAttributeType type; }
            int pointer = IntPtr.Size;
            int attributesOffset = Align(pointer + sizeof(int), pointer);
            int attributeSize = Align(pointer + sizeof(int), pointer);
            int reservedOffset = attributesOffset + (SchemaAttributeCapacity * attributeSize);
            int total = Align(Align(reservedOffset + sizeof(int), pointer) + (SchemaReservedPointers * pointer), pointer);

            _memory = Marshal.AllocHGlobal(total);
            try
            {
                Marshal.Copy(new byte[total], 0, _memory, total);
                Marshal.WriteIntPtr(_memory, 0, Own(LibsecretAttributes.SchemaName));
                Marshal.WriteInt32(_memory, pointer, 0);
                Marshal.WriteIntPtr(_memory, attributesOffset, Own(LibsecretAttributes.AppKey));
                Marshal.WriteInt32(_memory, attributesOffset + pointer, 0);
                Marshal.WriteIntPtr(_memory, attributesOffset + attributeSize, Own(LibsecretAttributes.NameKey));
                Marshal.WriteInt32(_memory, attributesOffset + attributeSize + pointer, 0);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IntPtr Pointer => _memory;

        public void Dispose()
        {
            foreach (NativeUtf8 text in _strings)
            {
                text.Dispose();
            }

            _strings.Clear();
            IntPtr memory = Interlocked.Exchange(ref _memory, IntPtr.Zero);
            if (memory != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(memory);
            }
        }

        private IntPtr Own(string text)
        {
            NativeUtf8 native = NativeUtf8.Create(text, nameof(text));
            _strings.Add(native);
            return native.Pointer;
        }
    }

    /// <summary>A <c>GHashTable</c> of string keys and values. The key and value strings are owned here and outlive the table.</summary>
    [SuppressMessage("Usage", "CA2216:Disposable types should declare finalizer", Justification = "The table is created and disposed inside one call under a using declaration; a finalizer would call into GLib from the finalizer thread.")]
    private sealed class AttributeTable : IDisposable
    {
        private readonly List<NativeUtf8> _strings = [];
        private IntPtr _table;

        public AttributeTable(NativeSymbols symbols, (string Key, string Value)[] pairs)
        {
            _table = LibsecretNativeMethods.HashTableNew(symbols.HashFunction, symbols.EqualFunction);
            try
            {
                foreach ((string key, string value) in pairs)
                {
                    NativeUtf8 nativeKey = NativeUtf8.Create(key, "name");
                    _strings.Add(nativeKey);
                    NativeUtf8 nativeValue = NativeUtf8.Create(value, "name");
                    _strings.Add(nativeValue);
                    _ = LibsecretNativeMethods.HashTableInsert(_table, nativeKey.Pointer, nativeValue.Pointer);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IntPtr Pointer => _table;

        public void Dispose()
        {
            IntPtr table = Interlocked.Exchange(ref _table, IntPtr.Zero);
            if (table != IntPtr.Zero)
            {
                LibsecretNativeMethods.HashTableUnref(table);
            }

            foreach (NativeUtf8 text in _strings)
            {
                text.Dispose();
            }

            _strings.Clear();
        }
    }

    /// <summary>A <c>GCancellable</c> that is cancelled when the token is. Disposing waits for a running cancel callback before the object is released.</summary>
    [SuppressMessage("Usage", "CA2216:Disposable types should declare finalizer", Justification = "The scope is created and disposed inside one call under a using declaration; a finalizer would call into GLib from the finalizer thread.")]
    private sealed class CancellableScope : IDisposable
    {
        private readonly CancellationTokenRegistration _registration;
        private IntPtr _handle;

        public CancellableScope(CancellationToken cancellationToken)
        {
            _handle = LibsecretNativeMethods.CancellableNew();
            _registration = cancellationToken.Register(static state => LibsecretNativeMethods.CancellableCancel((IntPtr)state!), _handle);
        }

        public IntPtr Handle => _handle;

        public void Dispose()
        {
            _registration.Dispose();
            IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
            {
                LibsecretNativeMethods.ObjectUnref(handle);
            }
        }
    }
}
