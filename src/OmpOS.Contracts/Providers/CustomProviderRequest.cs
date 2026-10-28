using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>
/// The body of <c>POST /providers/custom</c>: an OpenAI- or Anthropic-compatible endpoint. The base URL must be https, except for a host on
/// this machine; a link-local or metadata address, a name for one and a URL with user information are refused.
/// </summary>
public sealed record CustomProviderRequest
{
    /// <summary>Gets the provider id: lower-case letters, digits, dot, underscore and hyphen, at most 64 characters. Required.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>Gets the name shown for the endpoint, at most 80 characters. Defaults to the id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    /// <summary>Gets the wire protocol. Required.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CustomProviderProtocol? Protocol { get; init; }

    /// <summary>Gets the base URL. Required.</summary>
    [JsonPropertyName("baseUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaseAddress { get; init; }

    /// <summary>Gets the model ids, at least one and at most 50.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? ModelIds { get; init; }

    /// <summary>Gets a value indicating whether the endpoint needs a key. Defaults to true; the key itself is never part of this request.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? KeyRequired { get; init; }
}
