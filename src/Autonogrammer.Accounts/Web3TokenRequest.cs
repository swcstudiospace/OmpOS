using System.Text.Json.Serialization;

namespace Autonogrammer.Accounts;

/// <summary>
/// Body of <c>POST /token?grant_type=web3</c>: a signed Sign-In-With-Ethereum message, its signature and the chain. The service documents the
/// <c>chain</c>, <c>message</c> and <c>signature</c> members; <c>gotrue_meta_security</c> carries the CAPTCHA token only when the project enables CAPTCHA.
/// </summary>
/// <param name="Message">The EIP-4361 message exactly as the wallet signed it.</param>
/// <param name="Signature">The hexadecimal EIP-191 signature of the message.</param>
/// <param name="Chain">The chain the message is for: <c>ethereum</c>.</param>
/// <param name="GoTrueMetaSecurity">The CAPTCHA token wrapper, or null when no token is sent.</param>
internal sealed record Web3TokenRequest(
    string Message,
    string Signature,
    string Chain,
    [property: JsonPropertyName("gotrue_meta_security")] GoTrueSecurity? GoTrueMetaSecurity);
