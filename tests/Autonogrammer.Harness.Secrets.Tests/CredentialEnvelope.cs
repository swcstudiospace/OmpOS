namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>A named provider credential whose key is a secret, as a settings object would carry it.</summary>
/// <param name="Name">The provider name.</param>
/// <param name="Key">The provider key.</param>
internal sealed record CredentialEnvelope(string Name, SecretString Key);
