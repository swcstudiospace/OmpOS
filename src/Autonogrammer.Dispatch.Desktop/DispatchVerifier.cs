using System.Globalization;
using System.Security.Cryptography;
using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Verifies one hub command before anything runs. Canonical bytes are built only by <see cref="DispatchCanonical"/>, so this verifier
/// and the hub agree byte for byte. Every check runs before a handler can see the command; a refused frame has no effect.
/// </summary>
public sealed class DispatchVerifier
{
    /// <summary>The allowed skew between the hub clock and the frame expiry, in seconds.</summary>
    public const int ClockSkewSeconds = 30;

    private readonly DispatchKeyRing _keys;
    private readonly string _accountId;
    private readonly string _deviceId;
    private readonly TimeProvider _clock;

    /// <summary>Creates the verifier for one paired device.</summary>
    /// <param name="keys">The keys delivered at pairing.</param>
    /// <param name="accountId">The account stored at pairing. Frames for any other user are refused.</param>
    /// <param name="deviceId">The device this desktop is paired as.</param>
    /// <param name="clock">The local clock.</param>
    public DispatchVerifier(DispatchKeyRing keys, string accountId, string deviceId, TimeProvider clock)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _accountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Verifies one command frame.</summary>
    /// <param name="frame">The decoded frame.</param>
    /// <param name="clockOffset">The hub clock minus the local clock, measured at the welcome frame.</param>
    /// <returns>The verdict. Only an accepted verdict may lead to execution.</returns>
    public DispatchVerdict Verify(DispatchCmdFrame frame, TimeSpan clockOffset)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.V != 1 || !DispatchOpWire.TryParse(frame.Op, out _) || !DispatchCapabilityWire.TryParse(frame.Capability, out var capability)
            || !DispatchCapabilityWire.IsCommandCapability(capability))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.Malformed);
        }

        if (!TryBuildCanonical(frame, out var canonical))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.Malformed);
        }

        if (!string.Equals(frame.UserId, _accountId, StringComparison.Ordinal))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.WrongUser);
        }

        if (!string.Equals(frame.DeviceId, _deviceId, StringComparison.Ordinal))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.WrongDevice);
        }

        if (!_keys.TryGet(frame.Kid, out var key) || key is null)
        {
            return DispatchVerdict.Refuse(DispatchRefusal.UnknownKid);
        }

        var hubNow = _clock.GetUtcNow().ToUnixTimeSeconds() + (long)Math.Floor(clockOffset.TotalSeconds);
        if (frame.ExpiresAt + ClockSkewSeconds < hubNow)
        {
            return DispatchVerdict.Refuse(DispatchRefusal.Expired);
        }

        if (!DispatchBase64Url.TryDecode(frame.Payload, out var payload))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.PayloadMismatch);
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(payload));
        if (!string.Equals(digest, frame.PayloadSha256, StringComparison.Ordinal))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.PayloadMismatch);
        }

        if (!DispatchBase64Url.TryDecode(frame.Sig, out var signature) || signature.Length != 64
            || !key.VerifyData(canonical, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            return DispatchVerdict.Refuse(DispatchRefusal.SignatureInvalid);
        }

        return DispatchVerdict.Accept;
    }

    private static bool TryBuildCanonical(DispatchCmdFrame frame, out byte[] canonical)
    {
        canonical = [];
        try
        {
            canonical = DispatchCanonical.Build(new DispatchCanonicalInput(
                frame.V.ToString(CultureInfo.InvariantCulture),
                frame.CommandId,
                frame.Seq.ToString(CultureInfo.InvariantCulture),
                frame.UserId,
                frame.DeviceId,
                frame.Capability,
                frame.Op,
                frame.IssuedAt.ToString(CultureInfo.InvariantCulture),
                frame.ExpiresAt.ToString(CultureInfo.InvariantCulture),
                frame.Nonce,
                frame.PayloadSha256,
                frame.Kid));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
