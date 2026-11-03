using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop.Tests.Support;

/// <summary>Signs command frames the way the hub does, so tests exercise the verifier against real signatures.</summary>
internal sealed class DispatchTestSigner : IDisposable
{
    public const string Kid = "hub.key-1";
    public const string AccountId = "acct_1";
    public const string DeviceId = "device_2";

    public DispatchTestSigner()
    {
        Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    public ECDsa Key { get; }

    public DispatchPublicKey PublicKey => new(Kid, Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo()));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Builds and signs one command frame.</summary>
    public DispatchCmdFrame Sign(
        long seq,
        long issuedAt,
        long expiresAt,
        string commandId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        string nonce = "nonce-1",
        string payloadJson = "{\"text\":\"hello\"}",
        string op = "session.list",
        string capability = "sessions",
        string kid = Kid,
        string userId = AccountId,
        string deviceId = DeviceId,
        ECDsa? signer = null)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var payloadSha = Convert.ToHexStringLower(SHA256.HashData(payload));
        var canonical = DispatchCanonical.Build(new DispatchCanonicalInput(
            "1",
            commandId,
            seq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            userId,
            deviceId,
            capability,
            op,
            issuedAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            expiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            nonce,
            payloadSha,
            kid));
        var signature = (signer ?? Key).SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new DispatchCmdFrame
        {
            V = 1,
            T = "cmd",
            CommandId = commandId,
            Seq = seq,
            UserId = userId,
            DeviceId = deviceId,
            Capability = capability,
            Op = op,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Nonce = nonce,
            Payload = Base64Url(payload),
            PayloadSha256 = payloadSha,
            Kid = kid,
            Sig = Base64Url(signature),
        };
    }

    /// <summary>Serialises a frame as the hub sends it on the wire.</summary>
    public static byte[] Wire(DispatchCmdFrame frame) =>
        JsonSerializer.SerializeToUtf8Bytes(frame, DispatchJsonContext.Default.DispatchCmdFrame);

    /// <summary>Builds an unsigned-looking compact JWT whose payload carries the given expiry.</summary>
    public static string Jwt(DateTimeOffset expiresAt)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes("{\"exp\":" + expiresAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"));
        return header + "." + payload + ".c2ln";
    }

    public void Dispose() => Key.Dispose();
}
