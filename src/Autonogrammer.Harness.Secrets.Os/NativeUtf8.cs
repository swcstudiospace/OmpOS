using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// A NUL-terminated UTF-8 copy of a string in unmanaged memory, written by hand so the native copy can be zeroed before it is freed.
/// The managed staging buffer is zeroed as soon as the copy exists.
/// </summary>
internal sealed class NativeUtf8 : IDisposable
{
    private IntPtr _pointer;

    private NativeUtf8(IntPtr pointer, int length)
    {
        _pointer = pointer;
        Length = length;
    }

    /// <summary>Gets the number of bytes before the terminating NUL.</summary>
    public int Length { get; }

    /// <summary>Gets the address of the first byte.</summary>
    /// <exception cref="ObjectDisposedException">The copy has been zeroed and freed.</exception>
    public IntPtr Pointer => _pointer != IntPtr.Zero ? _pointer : throw new ObjectDisposedException(nameof(NativeUtf8));

    /// <summary>Copies <paramref name="text"/> into unmanaged memory as UTF-8 followed by a NUL.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="parameterName">The parameter name reported when the text cannot be represented. The text itself is never reported.</param>
    /// <returns>The native copy. Dispose it to zero and free the memory.</returns>
    /// <exception cref="ArgumentException">The text contains a NUL character or a lone surrogate.</exception>
    public static NativeUtf8 Create(string text, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("The text must not contain a NUL character.", parameterName);
        }

        var staging = new byte[Encoding.UTF8.GetMaxByteCount(text.Length)];
        try
        {
            // Utf8.FromUtf16 reports a lone surrogate as a status instead of throwing, so no exception message can quote a code unit of the text.
            if (Utf8.FromUtf16(text, staging, out _, out int length, replaceInvalidSequences: false, isFinalBlock: true) != OperationStatus.Done)
            {
                throw new ArgumentException("The text must be valid Unicode.", parameterName);
            }

            IntPtr memory = Marshal.AllocHGlobal(length + 1);
            Marshal.Copy(staging, 0, memory, length);
            Marshal.WriteByte(memory, length, 0);
            return new NativeUtf8(memory, length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(staging);
        }
    }

    /// <summary>Reads a NUL-terminated UTF-8 string from native memory.</summary>
    /// <param name="pointer">The address of the first byte, or zero.</param>
    /// <returns>The string, or null when <paramref name="pointer"/> is zero.</returns>
    public static string? Read(IntPtr pointer) => pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);

    /// <summary>Zeroes and frees the native copy.</summary>
    public void Dispose()
    {
        IntPtr memory = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
        if (memory == IntPtr.Zero)
        {
            return;
        }

        Marshal.Copy(new byte[Length + 1], 0, memory, Length + 1);
        Marshal.FreeHGlobal(memory);
    }
}
