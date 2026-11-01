using System.Globalization;
using System.Text;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Canonical UTF-8 bytes a hub signs and a device verifies. Fixed order, LF line ends, no other whitespace.
/// A value outside <c>[A-Za-z0-9._:-]</c> is refused before bytes are built and before bytes are parsed.
/// </summary>
public static class DispatchCanonical
{
    /// <summary>First line of every command signature.</summary>
    public const string Header = "autonogrammer-dispatch-cmd-v1";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Reports whether a signed value is non-empty and inside the closed charset.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when every character is in <c>[A-Za-z0-9._:-]</c>.</returns>
    public static bool IsSignedValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!IsSignedChar(ch))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds the canonical bytes. Throws before any bytes are returned when a value is outside the charset.</summary>
    /// <param name="input">The signed fields, already rendered as text.</param>
    /// <returns>UTF-8 canonical bytes, including the trailing LF.</returns>
    public static byte[] Build(DispatchCanonicalInput input)
    {
        Validate(input);
        var text = Header + "\n"
            + "v=" + input.Version + "\n"
            + "commandId=" + input.CommandId + "\n"
            + "seq=" + input.Seq + "\n"
            + "userId=" + input.UserId + "\n"
            + "deviceId=" + input.DeviceId + "\n"
            + "capability=" + input.Capability + "\n"
            + "op=" + input.Op + "\n"
            + "issuedAt=" + input.IssuedAt + "\n"
            + "expiresAt=" + input.ExpiresAt + "\n"
            + "nonce=" + input.Nonce + "\n"
            + "payloadSha256=" + input.PayloadSha256 + "\n"
            + "kid=" + input.KeyId + "\n";
        return Utf8.GetBytes(text);
    }

    /// <summary>Builds canonical bytes from typed command fields.</summary>
    /// <param name="version">Protocol version.</param>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="seq">Per-device sequence.</param>
    /// <param name="userId">Account identifier.</param>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="capability">Command capability.</param>
    /// <param name="op">Operation.</param>
    /// <param name="issuedAt">Issued-at Unix seconds.</param>
    /// <param name="expiresAt">Expires-at Unix seconds.</param>
    /// <param name="nonce">Delivery nonce.</param>
    /// <param name="payloadSha256">Hex SHA-256 of the decoded payload.</param>
    /// <param name="keyId">Signing key identifier.</param>
    /// <returns>UTF-8 canonical bytes.</returns>
    public static byte[] Build(
        int version,
        string commandId,
        long seq,
        string userId,
        string deviceId,
        DispatchCapability capability,
        DispatchOp op,
        long issuedAt,
        long expiresAt,
        string nonce,
        string payloadSha256,
        string keyId) =>
        Build(new DispatchCanonicalInput(
            version.ToString(CultureInfo.InvariantCulture),
            commandId,
            seq.ToString(CultureInfo.InvariantCulture),
            userId,
            deviceId,
            DispatchCapabilityWire.ToWire(capability),
            DispatchOpWire.ToWire(op),
            issuedAt.ToString(CultureInfo.InvariantCulture),
            expiresAt.ToString(CultureInfo.InvariantCulture),
            nonce,
            payloadSha256,
            keyId));

    /// <summary>Parses canonical bytes and refuses any value outside the charset. This is the verify direction.</summary>
    /// <param name="utf8">The canonical bytes.</param>
    /// <returns>The signed fields.</returns>
    public static DispatchCanonicalInput Parse(ReadOnlySpan<byte> utf8)
    {
        var text = Utf8.GetString(utf8);
        var lines = text.Split('\n');
        if (lines.Length != 14 || lines[13].Length != 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
        {
            throw new ArgumentException("Canonical bytes are not the fixed command form.", nameof(utf8));
        }

        var input = new DispatchCanonicalInput(
            Value(lines[1], "v"),
            Value(lines[2], "commandId"),
            Value(lines[3], "seq"),
            Value(lines[4], "userId"),
            Value(lines[5], "deviceId"),
            Value(lines[6], "capability"),
            Value(lines[7], "op"),
            Value(lines[8], "issuedAt"),
            Value(lines[9], "expiresAt"),
            Value(lines[10], "nonce"),
            Value(lines[11], "payloadSha256"),
            Value(lines[12], "kid"));
        Validate(input);
        return input;
    }

    private static void Validate(DispatchCanonicalInput input)
    {
        Require(input.Version, nameof(input.Version));
        Require(input.CommandId, nameof(input.CommandId));
        Require(input.Seq, nameof(input.Seq));
        Require(input.UserId, nameof(input.UserId));
        Require(input.DeviceId, nameof(input.DeviceId));
        Require(input.Capability, nameof(input.Capability));
        Require(input.Op, nameof(input.Op));
        Require(input.IssuedAt, nameof(input.IssuedAt));
        Require(input.ExpiresAt, nameof(input.ExpiresAt));
        Require(input.Nonce, nameof(input.Nonce));
        Require(input.PayloadSha256, nameof(input.PayloadSha256));
        Require(input.KeyId, nameof(input.KeyId));
    }

    private static string Value(string line, string name)
    {
        var prefix = name + "=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal) || line.Contains(' ', StringComparison.Ordinal))
        {
            throw new ArgumentException("Canonical line " + name + " is missing or has whitespace.", nameof(line));
        }

        return line[prefix.Length..];
    }

    private static void Require(string value, string name)
    {
        if (!IsSignedValue(value))
        {
            throw new ArgumentOutOfRangeException(name, "Signed value is outside [A-Za-z0-9._:-].");
        }
    }

    private static bool IsSignedChar(char ch) =>
        ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or ':' or '-';
}
