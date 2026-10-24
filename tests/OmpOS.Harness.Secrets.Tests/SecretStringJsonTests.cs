using System.Text.Json;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Secrets.Tests;

/// <summary>
/// Verifies that a <see cref="SecretString"/> serialises as the redaction marker through the source-generated context,
/// and that JSON can never produce one.
/// </summary>
public sealed class SecretStringJsonTests
{
    private const string Value = "fixture-secret-7f3a9c2e51d84b06";

    [Fact]
    public void SerialisesAsTheRedactionMarkerNeverTheValue()
    {
        var secret = new SecretString(Value);

        string json = JsonSerializer.Serialize(secret, SecretTestJsonContext.Default.SecretString);

        json.ShouldBe($"\"{SecretString.RedactionMarker}\"");
        json.ShouldNotContain(Value);
    }

    [Fact]
    public void ASecretInsideAnObjectSerialisesAsTheMarkerNextToOtherFields()
    {
        var envelope = new CredentialEnvelope("openai", new SecretString(Value));

        string json = JsonSerializer.Serialize(envelope, SecretTestJsonContext.Default.CredentialEnvelope);

        json.ShouldBe($"{{\"Name\":\"openai\",\"Key\":\"{SecretString.RedactionMarker}\"}}");
        json.ShouldNotContain(Value);
    }

    [Fact]
    public void ReadingASecretFromAStringTokenIsRefused()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize("\"fixture-secret-7f3a\"", SecretTestJsonContext.Default.SecretString));
    }

    [Fact]
    public void ReadingASecretFromANullTokenIsRefused()
    {
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize("null", SecretTestJsonContext.Default.SecretString));
    }

    [Fact]
    public void AnObjectCarryingASecretCannotBeReadBack()
    {
        const string json = "{\"Name\":\"openai\",\"Key\":\"fixture-secret-7f3a\"}";

        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize(json, SecretTestJsonContext.Default.CredentialEnvelope));
    }
}
