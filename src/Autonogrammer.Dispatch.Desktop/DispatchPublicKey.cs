namespace Autonogrammer.Dispatch.Desktop;

/// <summary>A hub signing key delivered at pairing: its identifier and its SubjectPublicKeyInfo, base64 encoded.</summary>
/// <param name="Kid">The key identifier the hub writes in every command.</param>
/// <param name="SpkiBase64">The P-256 public key as SubjectPublicKeyInfo DER, base64 encoded.</param>
public sealed record DispatchPublicKey(string Kid, string SpkiBase64);
