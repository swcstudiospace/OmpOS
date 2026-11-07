using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Signs one command at delivery over the canonical bytes of its stored body (D-17). Nothing about the signature is stored, so a redelivery after
/// a key rotation signs with the current key.
/// </summary>
internal sealed class DispatchSigner
{
    private const int CanonicalVersion = 1;
    private readonly DeviceCredentialIssuer _issuer;

    /// <summary>Initializes a new instance of the <see cref="DispatchSigner"/> class.</summary>
    /// <param name="issuer">The credential issuer, which holds the command signing key.</param>
    public DispatchSigner(DeviceCredentialIssuer issuer)
    {
        _issuer = issuer;
    }

    /// <summary>Builds the signed command frame for one stored command.</summary>
    /// <param name="owner">The account that owns the command.</param>
    /// <param name="command">The command row.</param>
    /// <param name="body">The stored body, as text.</param>
    /// <param name="nonce">A fresh delivery nonce.</param>
    /// <returns>The frame, with its signature over the canonical bytes.</returns>
    public DispatchCmdFrame Sign(string owner, DispatchCommandRecord command, string body, string nonce)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var payloadSha256 = Convert.ToHexStringLower(SHA256.HashData(bodyBytes));
        var kid = _issuer.HubKey().Kid;
        var issuedAt = command.IssuedAt.ToUnixTimeSeconds();
        var expiresAt = command.ExpiresAt.ToUnixTimeSeconds();
        var canonical = DispatchCanonical.Build(CanonicalVersion, command.CommandId, command.Seq, owner, command.DeviceId, command.Capability, command.Op, issuedAt, expiresAt, nonce, payloadSha256, kid);
        return new DispatchCmdFrame
        {
            V = 1,
            T = DispatchFrameTypes.Cmd,
            CommandId = command.CommandId,
            Seq = command.Seq,
            UserId = owner,
            DeviceId = command.DeviceId,
            Capability = DispatchCapabilityWire.ToWire(command.Capability),
            Op = DispatchOpWire.ToWire(command.Op),
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Nonce = nonce,
            Payload = Base64UrlEncoder.Encode(bodyBytes),
            PayloadSha256 = payloadSha256,
            Kid = kid,
            Sig = Base64UrlEncoder.Encode(_issuer.SignCommand(canonical)),
        };
    }
}
