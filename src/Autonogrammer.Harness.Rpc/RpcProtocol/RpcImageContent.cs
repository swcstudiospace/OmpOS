namespace Autonogrammer.Harness.Rpc;

/// <summary>An image attached to a prompt, as base64 data.</summary>
public sealed record RpcImageContent
{
    /// <summary>Gets the content discriminator; always <c>image</c>.</summary>
    public string Type { get; } = "image";

    /// <summary>Gets the base64-encoded image bytes.</summary>
    public required string Data { get; init; }

    /// <summary>Gets the image MIME type, for example <c>image/png</c>.</summary>
    public required string MimeType { get; init; }
}
