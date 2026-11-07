using System.Reflection;
using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="IKeychainApi"/> over Keychain Services and CoreFoundation, loaded by absolute framework path with
/// <see cref="NativeLibrary.TryLoad(string, Assembly, DllImportSearchPath?, out IntPtr)"/>, so a host without the frameworks gets a
/// <see cref="DllNotFoundException"/>. Strings are marshalled by hand; the native UTF-8 copy and the mutable <c>CFData</c> that carries a secret
/// are zeroed before they are released. This code has been compiled but not run: this repository builds it on Linux only.
/// </summary>
internal sealed class KeychainNativeApi : IKeychainApi
{
    internal const string ServiceName = "Autonogrammer";

    private const uint Utf8Encoding = 0x08000100;

    private static readonly Lock Gate = new();
    private static Symbols? s_symbols;

    /// <inheritdoc />
    public int Add(string account, SecretString secret)
    {
        Symbols symbols = Load();
        using var scope = new CfScope();
        IntPtr attributes = scope.Dictionary(
            (symbols.SecClass, symbols.SecClassGenericPassword),
            (symbols.SecAttrService, scope.String(ServiceName)),
            (symbols.SecAttrAccount, scope.String(account)),
            (symbols.SecValueData, scope.Data(secret)));
        return KeychainNativeMethods.ItemAdd(attributes, IntPtr.Zero);
    }

    /// <inheritdoc />
    public int Update(string account, SecretString secret)
    {
        Symbols symbols = Load();
        using var scope = new CfScope();
        IntPtr query = Identify(symbols, scope, account);
        IntPtr changes = scope.Dictionary((symbols.SecValueData, scope.Data(secret)));
        return KeychainNativeMethods.ItemUpdate(query, changes);
    }

    /// <inheritdoc />
    public int Copy(string account, out SecretString? secret)
    {
        secret = null;
        Symbols symbols = Load();
        using var scope = new CfScope();
        IntPtr query = scope.Dictionary(
            (symbols.SecClass, symbols.SecClassGenericPassword),
            (symbols.SecAttrService, scope.String(ServiceName)),
            (symbols.SecAttrAccount, scope.String(account)),
            (symbols.SecReturnData, symbols.True),
            (symbols.SecMatchLimit, symbols.SecMatchLimitOne));
        int status = KeychainNativeMethods.ItemCopyMatching(query, out IntPtr data);
        if (status != 0 || data == IntPtr.Zero)
        {
            return status;
        }

        try
        {
            int length = checked((int)KeychainNativeMethods.DataGetLength(data));
            string? text = length == 0 ? null : Marshal.PtrToStringUTF8(KeychainNativeMethods.DataGetBytePtr(data), length);
            secret = string.IsNullOrEmpty(text) ? null : new SecretString(text);
        }
        finally
        {
            KeychainNativeMethods.Release(data);
        }

        return status;
    }

    /// <inheritdoc />
    public int Delete(string account)
    {
        Symbols symbols = Load();
        using var scope = new CfScope();
        return KeychainNativeMethods.ItemDelete(Identify(symbols, scope, account));
    }

    /// <inheritdoc />
    public int List(out IReadOnlyList<string> accounts)
    {
        accounts = [];
        Symbols symbols = Load();
        using var scope = new CfScope();
        IntPtr query = scope.Dictionary(
            (symbols.SecClass, symbols.SecClassGenericPassword),
            (symbols.SecAttrService, scope.String(ServiceName)),
            (symbols.SecReturnAttributes, symbols.True),
            (symbols.SecMatchLimit, symbols.SecMatchLimitAll));
        int status = KeychainNativeMethods.ItemCopyMatching(query, out IntPtr array);
        if (status != 0 || array == IntPtr.Zero)
        {
            return status;
        }

        try
        {
            List<string> found = [];
            long count = KeychainNativeMethods.ArrayGetCount(array).ToInt64();
            for (long index = 0; index < count; index++)
            {
                IntPtr item = KeychainNativeMethods.ArrayGetValueAtIndex(array, new IntPtr(index));
                IntPtr account = KeychainNativeMethods.DictionaryGetValue(item, symbols.SecAttrAccount);
                if (account != IntPtr.Zero && ReadString(account) is { Length: > 0 } name)
                {
                    found.Add(name);
                }
            }

            accounts = found;
        }
        finally
        {
            KeychainNativeMethods.Release(array);
        }

        return status;
    }

    private static IntPtr Identify(Symbols symbols, CfScope scope, string account) => scope.Dictionary(
        (symbols.SecClass, symbols.SecClassGenericPassword),
        (symbols.SecAttrService, scope.String(ServiceName)),
        (symbols.SecAttrAccount, scope.String(account)));

    private static string? ReadString(IntPtr text)
    {
        IntPtr capacity = new(KeychainNativeMethods.StringGetMaximumSizeForEncoding(KeychainNativeMethods.StringGetLength(text), Utf8Encoding).ToInt64() + 1);
        IntPtr buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            return KeychainNativeMethods.StringGetCString(text, buffer, capacity, Utf8Encoding) != 0 ? Marshal.PtrToStringUTF8(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Symbols Load()
    {
        lock (Gate)
        {
            if (s_symbols is not null)
            {
                return s_symbols;
            }

            Assembly assembly = typeof(KeychainNativeApi).Assembly;
            if (!NativeLibrary.TryLoad(KeychainNativeMethods.CoreFoundationLibrary, assembly, DllImportSearchPath.SafeDirectories, out IntPtr coreFoundation))
            {
                throw new DllNotFoundException("The CoreFoundation framework could not be loaded.");
            }

            if (!NativeLibrary.TryLoad(KeychainNativeMethods.SecurityLibrary, assembly, DllImportSearchPath.SafeDirectories, out IntPtr security))
            {
                throw new DllNotFoundException("The Security framework could not be loaded.");
            }

            // kSec... and kCFBooleanTrue are exported CFTypeRef variables: the export is the address of the pointer. The two callback tables are
            // exported structs: their address is the argument.
            s_symbols = new Symbols(
                SecClass: Constant(security, "kSecClass"),
                SecClassGenericPassword: Constant(security, "kSecClassGenericPassword"),
                SecAttrService: Constant(security, "kSecAttrService"),
                SecAttrAccount: Constant(security, "kSecAttrAccount"),
                SecValueData: Constant(security, "kSecValueData"),
                SecReturnData: Constant(security, "kSecReturnData"),
                SecReturnAttributes: Constant(security, "kSecReturnAttributes"),
                SecMatchLimit: Constant(security, "kSecMatchLimit"),
                SecMatchLimitOne: Constant(security, "kSecMatchLimitOne"),
                SecMatchLimitAll: Constant(security, "kSecMatchLimitAll"),
                True: Constant(coreFoundation, "kCFBooleanTrue"),
                KeyCallbacks: NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks"),
                ValueCallbacks: NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks"));
            return s_symbols;
        }
    }

    private static IntPtr Constant(IntPtr library, string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));

    private sealed record Symbols(
        IntPtr SecClass,
        IntPtr SecClassGenericPassword,
        IntPtr SecAttrService,
        IntPtr SecAttrAccount,
        IntPtr SecValueData,
        IntPtr SecReturnData,
        IntPtr SecReturnAttributes,
        IntPtr SecMatchLimit,
        IntPtr SecMatchLimitOne,
        IntPtr SecMatchLimitAll,
        IntPtr True,
        IntPtr KeyCallbacks,
        IntPtr ValueCallbacks);

    /// <summary>Owns the CoreFoundation objects built for one call and releases them, zeroing the data objects that carry a secret first.</summary>
    private sealed class CfScope : IDisposable
    {
        private readonly List<IntPtr> _objects = [];
        private readonly List<IntPtr> _secretData = [];

        public IntPtr String(string text)
        {
            using NativeUtf8 native = NativeUtf8.Create(text, nameof(text));
            return Track(KeychainNativeMethods.StringCreateWithBytes(IntPtr.Zero, native.Pointer, new IntPtr(native.Length), Utf8Encoding, 0));
        }

        public IntPtr Data(SecretString secret)
        {
            using NativeUtf8 native = NativeUtf8.Create(secret.Reveal(), nameof(secret));
            IntPtr data = Track(KeychainNativeMethods.DataCreateMutable(IntPtr.Zero, IntPtr.Zero));
            _secretData.Add(data);
            KeychainNativeMethods.DataAppendBytes(data, native.Pointer, new IntPtr(native.Length));
            return data;
        }

        public IntPtr Dictionary(params (IntPtr Key, IntPtr Value)[] entries)
        {
            Symbols symbols = Load();
            IntPtr dictionary = Track(KeychainNativeMethods.DictionaryCreateMutable(IntPtr.Zero, new IntPtr(entries.Length), symbols.KeyCallbacks, symbols.ValueCallbacks));
            foreach ((IntPtr key, IntPtr value) in entries)
            {
                KeychainNativeMethods.DictionarySetValue(dictionary, key, value);
            }

            return dictionary;
        }

        public void Dispose()
        {
            foreach (IntPtr data in _secretData)
            {
                int length = checked((int)KeychainNativeMethods.DataGetLength(data));
                if (length > 0)
                {
                    Marshal.Copy(new byte[length], 0, KeychainNativeMethods.DataGetMutableBytePtr(data), length);
                }
            }

            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                KeychainNativeMethods.Release(_objects[index]);
            }

            _objects.Clear();
            _secretData.Clear();
        }

        private IntPtr Track(IntPtr instance)
        {
            if (instance == IntPtr.Zero)
            {
                throw new InvalidOperationException("CoreFoundation could not allocate an object.");
            }

            _objects.Add(instance);
            return instance;
        }
    }
}
