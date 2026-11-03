namespace Autonogrammer.Accounts;

/// <summary>The fields of one Sign-In-With-Ethereum message (EIP-4361).</summary>
/// <param name="Domain">The authority that asks for the signature, for example <c>example.com</c> or <c>example.com:8443</c>.</param>
/// <param name="Address">The wallet address, 0x followed by 40 hexadecimal digits.</param>
/// <param name="Uri">The URI of the sign-in, https or http to a loopback address.</param>
/// <param name="ChainId">The chain identifier, a positive decimal number, for example <c>1</c>.</param>
/// <param name="Nonce">A nonce of at least eight letters or digits, unique to this sign-in.</param>
/// <param name="IssuedAt">When the message was issued. It is written in UTC.</param>
/// <param name="Statement">An optional statement on one line, or null.</param>
public sealed record SiweFields(string Domain, string Address, Uri Uri, string ChainId, string Nonce, DateTimeOffset IssuedAt, string? Statement);
