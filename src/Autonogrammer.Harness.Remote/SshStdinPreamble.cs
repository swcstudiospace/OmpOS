using System.Globalization;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// The bytes written to the stdin of the ssh child before the first protocol frame: environment variables (which may be provider
/// tokens) and small files, read by the remote shell and never placed on a command line. The bytes are secret, so the type never
/// prints them.
/// </summary>
public sealed class SshStdinPreamble
{
    /// <summary>Initializes a new instance of the <see cref="SshStdinPreamble"/> class.</summary>
    /// <param name="bytes">The UTF-8 bytes to write.</param>
    internal SshStdinPreamble(ReadOnlyMemory<byte> bytes) => Bytes = bytes;

    /// <summary>Gets the bytes to write to the child's stdin. They may contain secrets.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Describes the preamble by its size only.</summary>
    /// <returns>The description, without any content.</returns>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"SshStdinPreamble {{ Bytes = {Bytes.Length} }}");
}
