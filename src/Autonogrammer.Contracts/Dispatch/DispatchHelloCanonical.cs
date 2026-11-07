using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Canonical UTF-8 bytes a device signs for its hello and the hub rebuilds to verify it (D-14). The origin enters the bytes as its SHA-256 in
/// lowercase hexadecimal, because an origin holds characters outside the signed charset. The nonce and the device identifier must be inside
/// that charset.
/// </summary>
public static class DispatchHelloCanonical
{
    /// <summary>First line of every hello signature.</summary>
    public const string Header = "autonogrammer-dispatch-hello-v1";

    /// <summary>Builds the canonical bytes, including the trailing LF.</summary>
    /// <param name="nonce">The challenge nonce, base64url.</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="origin">The hub origin the device reached.</param>
    /// <param name="lastAppliedSeq">The last sequence the device applied.</param>
    /// <returns>UTF-8 canonical bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="origin"/> is null.</exception>
    /// <exception cref="ArgumentException">A signed value is outside the closed charset, or the sequence is negative.</exception>
    public static byte[] Build(string nonce, string deviceId, string origin, long lastAppliedSeq)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!DispatchCanonical.IsSignedValue(nonce) || !DispatchCanonical.IsSignedValue(deviceId))
        {
            throw new ArgumentException("The nonce and the device identifier must be inside the signed charset.", nameof(nonce));
        }

        if (lastAppliedSeq < 0)
        {
            throw new ArgumentException("The last applied sequence cannot be negative.", nameof(lastAppliedSeq));
        }

        var originDigest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin)));
        var text = Header + "\n"
            + "nonce=" + nonce + "\n"
            + "deviceId=" + deviceId + "\n"
            + "origin=" + originDigest + "\n"
            + "lastAppliedSeq=" + lastAppliedSeq.ToString(CultureInfo.InvariantCulture) + "\n";
        return Encoding.UTF8.GetBytes(text);
    }
}
