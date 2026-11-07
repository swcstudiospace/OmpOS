namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// An image a tool call returned (kind "image"). A placeholder has an empty <see cref="DataBase64"/> and
/// <see cref="Omitted"/> set, so a view never decodes it. <see cref="Description"/> is the alt text.
/// </summary>
public sealed record ImageBlock : UiBlock
{
    /// <summary>The accessible name used when the update carried no description.</summary>
    public const string FallbackAlt = "Image returned by tool";

    /// <summary>Gets the identifier of the tool call that returned the image.</summary>
    public required string CallId { get; init; }

    /// <summary>Gets the name of the tool that returned the image.</summary>
    public required string ToolName { get; init; }

    /// <summary>Gets the declared media type.</summary>
    public required string MimeType { get; init; }

    /// <summary>Gets the base64 payload, or an empty string when the image is a placeholder.</summary>
    public required string DataBase64 { get; init; }

    /// <summary>Gets the description the update carried, or null when it carried none.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the decoded byte length when the payload was kept, otherwise zero.</summary>
    public required int ByteLength { get; init; }

    /// <summary>Gets whether the payload was refused and must not be decoded.</summary>
    public required bool Omitted { get; init; }

    /// <summary>Gets the alt text: the description when it has content, otherwise <see cref="FallbackAlt"/>.</summary>
    public string AltText => string.IsNullOrWhiteSpace(Description) ? FallbackAlt : Description;

    /// <summary>Gets the inline data URI for a kept payload. A placeholder has no URI.</summary>
#pragma warning disable CA1056 // A data URI is text, not a navigable System.Uri.
    public string? DataUri => Omitted || DataBase64.Length == 0 ? null : $"data:{MimeType};base64,{DataBase64}";
#pragma warning restore CA1056

    /// <summary>Builds the image block both folds emit for one update image, so the identifier and placeholder rule match.</summary>
    /// <param name="callId">The tool call identifier.</param>
    /// <param name="toolName">The tool name.</param>
    /// <param name="index">The image's index in the update.</param>
    /// <param name="image">The image the update carried.</param>
    /// <returns>The block, with an empty payload when the image is a placeholder.</returns>
    public static ImageBlock FromUpdate(string callId, string toolName, int index, ToolUpdateImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var omitted = image.DataBase64.Length == 0;
        return new ImageBlock
        {
            Id = $"{callId}-image-{index}",
            CallId = callId,
            ToolName = toolName,
            MimeType = image.MimeType,
            DataBase64 = omitted ? string.Empty : image.DataBase64,
            Description = image.Description,
            ByteLength = omitted ? 0 : ToolUpdateContent.DecodedLength(image.DataBase64),
            Omitted = omitted,
        };
    }
}
