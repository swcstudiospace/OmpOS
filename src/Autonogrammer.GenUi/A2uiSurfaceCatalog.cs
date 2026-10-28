using System.Text.Json;

namespace Autonogrammer.GenUi;

/// <summary>
/// The fixed A2UI component-catalog subset the chat surface renders (UI-01).
/// </summary>
/// <remarks>
/// <para>
/// Only <c>heading</c>, <c>text</c>, <c>image</c>, <c>button</c>, and <c>list</c> render richly;
/// every other component type is returned with <see cref="SurfaceComponent.Supported"/> set to
/// <see langword="false"/> so the view can render a collapsed raw-JSON fallback instead of
/// blanking or throwing.
/// </para>
/// <para>
/// Accepted payload shape is a JSON object with an optional <c>components</c> array; a bare
/// single-component object (with a <c>type</c> member but no <c>components</c> array) is read as
/// one component. All parsing is guarded on <see cref="JsonValueKind"/> and never throws.
/// </para>
/// <para>
/// Images never fetch remote content: only an inline <c>data:</c> URI with a <c>image/png</c>,
/// <c>image/jpeg</c>, <c>image/gif</c>, or <c>image/webp</c> media type renders as an <c>img</c>;
/// every other source renders as a fallback link carrying the alt text (or the source text).
/// </para>
/// </remarks>
public static class A2uiSurfaceCatalog
{
    /// <summary>Gets the heading component type name.</summary>
    public const string HeadingType = "heading";

    /// <summary>Gets the text component type name.</summary>
    public const string TextType = "text";

    /// <summary>Gets the image component type name.</summary>
    public const string ImageType = "image";

    /// <summary>Gets the button component type name.</summary>
    public const string ButtonType = "button";

    /// <summary>Gets the list component type name.</summary>
    public const string ListType = "list";

    /// <summary>Gets the alt text an image receives when the agent supplies none or only white space.</summary>
    public const string DefaultImageAlt = "Image";

    /// <summary>Gets the supported component type names.</summary>
    public static IReadOnlyList<string> SupportedTypes { get; } =
        [HeadingType, TextType, ImageType, ButtonType, ListType];

    /// <summary>Determines whether a component type name is in the supported subset.</summary>
    /// <param name="type">The component type name.</param>
    /// <returns><see langword="true"/> when the surface renders the type richly.</returns>
    public static bool IsSupportedType(string? type) =>
        type is not null && SupportedTypes.Contains(type, StringComparer.Ordinal);

    /// <summary>
    /// Resolves the alt text every head gives an image. The agent's JSON may omit <c>alt</c>, and a head must never render an
    /// image without a text alternative, so a missing or white-space alt becomes <see cref="DefaultImageAlt"/>.
    /// </summary>
    /// <param name="alt">The alt text the payload carried, or null when it carried none.</param>
    /// <returns>The agent's alt text when it has any content; otherwise <see cref="DefaultImageAlt"/>.</returns>
    public static string ResolveImageAlt(string? alt) =>
        string.IsNullOrWhiteSpace(alt) ? DefaultImageAlt : alt;

    /// <summary>Parses the supported components of an A2UI surface payload. Never throws.</summary>
    /// <param name="payload">The raw A2UI payload.</param>
    /// <returns>The parsed components in payload order; empty when the payload carries none.</returns>
    public static IReadOnlyList<SurfaceComponent> GetComponents(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return ReadArray(payload);
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (payload.TryGetProperty("components", out var components)
            && components.ValueKind == JsonValueKind.Array)
        {
            return ReadArray(components);
        }

        if (payload.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String)
        {
            return [ReadOne(payload)];
        }

        return [];
    }

    private static List<SurfaceComponent> ReadArray(JsonElement array)
    {
        var result = new List<SurfaceComponent>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                result.Add(ReadOne(item));
            }
        }

        return result;
    }

    private static SurfaceComponent ReadOne(JsonElement element)
    {
        var type = element.TryGetProperty("type", out var typeElement)
            && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;

        var imageSource = ReadText(element, "url");
        var alt = ReadText(element, "alt");
        return new SurfaceComponent
        {
            Type = type,
            Supported = IsSupportedType(type),
            Text = ReadText(element, "text"),
            ImageUrl = ReadRenderableImageUri(imageSource),
            ImageSource = imageSource,
            ImageLinkHref = ReadLinkHref(imageSource),
            ImageLinkText = string.IsNullOrWhiteSpace(alt) ? imageSource : alt,
            Alt = alt,
            Label = ReadText(element, "label"),
            Items = ReadItems(element),
            Raw = element,
        };
    }

    private static string? ReadText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    /// <summary>
    /// Reads the renderable image URI: only an inline <c>data:</c> URI whose media type is
    /// <c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c>, or <c>image/webp</c>. Every other
    /// source (<c>http(s):</c>, relative, or any other scheme) yields null so the browser never
    /// fetches a remote image; those sources render as a fallback link instead.
    /// </summary>
    private static Uri? ReadRenderableImageUri(string? source)
    {
        if (string.IsNullOrEmpty(source) || !IsAllowedImageSource(source))
        {
            return null;
        }

        return Uri.TryCreate(source, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>Reads the fallback link target for a non-renderable image source: the source when its scheme is link-allowed, otherwise null.</summary>
    private static string? ReadLinkHref(string? source) =>
        string.IsNullOrEmpty(source) || !IsAllowedLinkScheme(source) ? null : source;

    /// <summary>Determines whether an image source renders as an <c>img</c>: an inline data URI with an allowed raster media type.</summary>
    /// <param name="source">The raw image source text.</param>
    /// <returns><see langword="true"/> for an allowed inline image; otherwise <see langword="false"/>.</returns>
    public static bool IsAllowedImageSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var text = source.TrimStart();
        return text.StartsWith("data:image/png;", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/png,", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/jpeg;", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/jpeg,", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/gif;", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/gif,", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/webp;", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:image/webp,", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Determines whether a URL keeps its href on a link: <c>http</c>, <c>https</c>, <c>mailto</c>, or a relative reference.</summary>
    /// <param name="source">The raw URL text.</param>
    /// <returns><see langword="true"/> when the href is kept; otherwise <see langword="false"/>.</returns>
    public static bool IsAllowedLinkScheme(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Browsers ignore ASCII whitespace inside the scheme, so strip it before classifying.
        var compact = string.Concat(source.Where(static c => !char.IsWhiteSpace(c)));
        var colon = compact.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return true;
        }

        var scheme = compact[..colon];
        return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ReadItems(JsonElement element)
    {
        if (!element.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(item.GetString() ?? string.Empty);
            }
        }

        return result;
    }
}
