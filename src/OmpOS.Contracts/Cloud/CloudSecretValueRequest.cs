using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Cloud;

/// <summary>The body of <c>PUT /cloud/secrets/{name}</c>. The value is write-only: no endpoint returns it.</summary>
public sealed record CloudSecretValueRequest
{
    /// <summary>Gets the secret value, 4 to 8 192 characters. Values shorter than 4 characters cannot be redacted reliably and are refused.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }
}
