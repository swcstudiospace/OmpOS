using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Acp;

/// <summary>The wire form of an embedded ACP resource: <c>{"uri","mimeType","text"}</c>.</summary>
public sealed class AcpEmbeddedResource
{
    /// <summary>Gets the resource location, serialized as <c>uri</c>.</summary>
    [JsonPropertyName("uri")]
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets the MIME type.</summary>
    public string MimeType { get; init; } = "text/plain";

    /// <summary>Gets the inlined resource text.</summary>
    public string Text { get; init; } = string.Empty;
}
