using System.Net;
using System.Text.RegularExpressions;
using Autonogrammer.GenUi;
using Markdig;
using Markdig.Extensions.GenericAttributes;
using Markdig.Extensions.MediaLinks;

namespace Autonogrammer.Presentation.Services;

/// <summary>
/// Renders markdown to sanitized HTML for <c>MarkupString</c> output.
/// </summary>
/// <remarks>
/// A single shared <see cref="MarkdownPipeline"/> renders the markdown with raw HTML disabled
/// and the advanced extensions enabled except two, so raw HTML in the source is escaped and never
/// reaches the output. Generic attributes (<c>{onclick="..."}</c>, <c>{style="..."}</c>, a second
/// <c>href</c>) and media links (a plain link to a video, an audio file or a video site becomes a
/// <c>video</c>, <c>audio</c> or <c>iframe</c> element) are off: model output is untrusted and the
/// sanitizer below looks only at anchors and images. Anchor hrefs are then allowlisted: only <c>http</c>, <c>https</c>, <c>mailto</c>,
/// and relative references keep their href; every other scheme loses the attribute. Images never
/// fetch remote content: only an inline <c>data:</c> URI with an allowed raster media type keeps
/// its <c>img</c>; every other source becomes a fallback link with the alt text (or the source
/// text) and <c>rel="noopener noreferrer nofollow"</c>.
/// </remarks>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static readonly Regex AnchorTag = new(
        "<a\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex ImgTag = new(
        "<img\\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex HrefAttribute = new(
        "\\s+href\\s*=\\s*(?<value>\"[^\"]*\"|'[^']*'|[^\\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex SrcAttribute = new(
        "\\s+src\\s*=\\s*(?<value>\"[^\"]*\"|'[^']*'|[^\\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex AltAttribute = new(
        "\\s+alt\\s*=\\s*(?<value>\"[^\"]*\"|'[^']*'|[^\\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
    /// <summary>Renders markdown to sanitized HTML.</summary>
    /// <param name="markdown">The markdown source.</param>
    /// <returns>Sanitized HTML safe to feed to <c>MarkupString</c>.</returns>
    public static string ToHtml(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var html = Markdig.Markdown.ToHtml(markdown, Pipeline);
        html = AnchorTag.Replace(html, static match => SanitizeAnchor(match.Value));
        return ImgTag.Replace(html, static match => SanitizeImage(match.Value));
    }

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder().DisableHtml().UseAdvancedExtensions();
        _ = builder.Extensions.TryRemove<GenericAttributesExtension>();
        _ = builder.Extensions.TryRemove<MediaLinkExtension>();
        return builder.Build();
    }

    // Every href of the tag is judged, not only the first: a browser keeps the first href it reads, so removing one rejected href must not promote a second.
    private static string SanitizeAnchor(string tag) =>
        HrefAttribute.Replace(tag, static match => IsAllowedHref(Unquote(match.Groups["value"].Value)) ? match.Value : string.Empty);

    private static string SanitizeImage(string tag)
    {
        var src = SrcAttribute.Match(tag) is { Success: true } srcMatch ? Unquote(srcMatch.Groups["value"].Value) : string.Empty;
        if (!string.IsNullOrEmpty(src) && A2uiSurfaceCatalog.IsAllowedImageSource(src))
        {
            return tag;
        }

        // The alt and src values come from Markdig-encoded attributes, so reusing them verbatim keeps the output encoded.
        var alt = AltAttribute.Match(tag) is { Success: true } altMatch ? Unquote(altMatch.Groups["value"].Value) : string.Empty;
        var text = string.IsNullOrEmpty(alt) ? src : alt;
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var href = !string.IsNullOrEmpty(src) && IsAllowedHref(src) ? $" href=\"{src}\"" : string.Empty;
        return $"<a rel=\"noopener noreferrer nofollow\" target=\"_blank\"{href}>{text}</a>";
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
            ? value[1..^1]
            : value;

    private static bool IsAllowedHref(string href) => A2uiSurfaceCatalog.IsAllowedLinkScheme(WebUtility.HtmlDecode(href));
}
