using System.Text.Json.Serialization;

namespace OmpOS.Harness.Secrets.Tests;

/// <summary>Source-generated JSON metadata for the wire shapes the secret tests serialise.</summary>
[JsonSerializable(typeof(SecretString))]
[JsonSerializable(typeof(CredentialEnvelope))]
internal sealed partial class SecretTestJsonContext : JsonSerializerContext
{
}
