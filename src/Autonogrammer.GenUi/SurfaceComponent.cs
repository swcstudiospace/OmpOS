using System.Text.Json;

namespace Autonogrammer.GenUi;

/// <summary>
/// One parsed A2UI surface component: either a supported catalog entry with its
/// extracted props, or an unsupported entry carrying its raw JSON for fallback rendering.
/// </summary>
public sealed record SurfaceComponent
{
    /// <summary>Gets the component type name as carried by the payload.</summary>
    public required string Type { get; init; }

    /// <summary>Gets whether <see cref="Type"/> is in the supported catalog subset.</summary>
    public required bool Supported { get; init; }

    /// <summary>Gets the heading or body text (heading, text).</summary>
    public string? Text { get; init; }

    /// <summary>Gets the renderable image URI (image): only an allowed inline data image, otherwise null so the browser never fetches a remote source.</summary>
    public Uri? ImageUrl { get; init; }

    /// <summary>Gets the raw image source text as carried by the payload (image), for the fallback link when <see cref="ImageUrl"/> is null.</summary>
    public string? ImageSource { get; init; }

    /// <summary>Gets the fallback link target for a non-renderable image source (image): the source when its scheme is link-allowed, otherwise null.</summary>
    public string? ImageLinkHref { get; init; }

    /// <summary>Gets the fallback link text for a non-renderable image source (image): the alt text, or the source text when alt is empty.</summary>
    public string? ImageLinkText { get; init; }

    /// <summary>Gets the image alt text (image).</summary>
    public string? Alt { get; init; }

    /// <summary>Gets the button label (button).</summary>
    public string? Label { get; init; }

    /// <summary>Gets the list items (list).</summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>Gets the component's raw JSON, preserved for fallback rendering.</summary>
    public required JsonElement Raw { get; init; }
}
