using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Autonogrammer.Presentation.Cowork.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// Builds the host document a surface loads for one version (D-23). The policy text comes only from
/// <see cref="ArtifactDocumentPolicy"/>; the meta element is always first. Html keeps scripts on, Svg is sanitised first, and
/// Mermaid uses the bundle from <see cref="IMermaidAssetProvider"/>, so a missing bundle yields
/// <see cref="ArtifactSurfaceReason.MermaidAssetMissing"/> and the caller shows the source.
/// </summary>
public static class ArtifactDocumentBuilder
{
    private const string DoctypePrefix = "<!DOCTYPE";

    /// <summary>Builds the document for one surface kind.</summary>
    /// <param name="content">The stored version.</param>
    /// <param name="approvedOrigins">The origins approved for this version. Empty means no network.</param>
    /// <param name="assets">The Mermaid bundle seam. Only Mermaid versions read it.</param>
    /// <returns>The document, or the reason it cannot be shown.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not shown in the surface: only Html, Svg and Mermaid are.</exception>
    public static ArtifactDocumentResult Build(ArtifactContent content, IReadOnlyList<string> approvedOrigins, IMermaidAssetProvider assets)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(approvedOrigins);
        ArgumentNullException.ThrowIfNull(assets);

        return content.Kind switch
        {
            ArtifactKind.Html => BuildHtml(content, approvedOrigins),
            ArtifactKind.Svg => BuildSvg(content, approvedOrigins),
            ArtifactKind.Mermaid => BuildMermaid(content, assets),
            _ => throw new ArgumentOutOfRangeException(nameof(content), content.Kind, "Only Html, Svg and Mermaid are shown in the surface."),
        };
    }

    private static ArtifactDocumentResult BuildHtml(ArtifactContent content, IReadOnlyList<string> approvedOrigins)
    {
        var html = Decode(content.Bytes);
        if (html is null)
        {
            return ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused);
        }

        var policy = ArtifactDocumentPolicy.ForHtml(scripts: true, approvedOrigins);
        return ArtifactDocumentResult.Ready(
            ArtifactDocumentPolicy.MetaElement(policy) + HtmlDocumentWrapper.DialogNoOpPrefix + StripDoctype(html));
    }

    private static ArtifactDocumentResult BuildSvg(ArtifactContent content, IReadOnlyList<string> approvedOrigins)
    {
        var sanitised = SvgSanitizer.Sanitize(content.Bytes.Span);
        if (!sanitised.Accepted || sanitised.Document is null)
        {
            return ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused);
        }

        var policy = ArtifactDocumentPolicy.ForSvgOrImage(approvedOrigins);
        return ArtifactDocumentResult.Ready(ArtifactDocumentPolicy.MetaElement(policy) + sanitised.Document);
    }

    private static ArtifactDocumentResult BuildMermaid(ArtifactContent content, IMermaidAssetProvider assets)
    {
        var diagram = Decode(content.Bytes);
        if (diagram is null)
        {
            return ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused);
        }

        var document = MermaidDocumentBuilder.Build(diagram, assets);
        return document.Available && document.Html is not null
            ? ArtifactDocumentResult.Ready(document.Html)
            : ArtifactDocumentResult.Refused(ArtifactSurfaceReason.MermaidAssetMissing);
    }

    /// <summary>Decodes stored bytes as strict UTF-8.</summary>
    /// <param name="bytes">The stored bytes.</param>
    /// <returns>The text, or null when the bytes are not valid UTF-8.</returns>
    internal static string? Decode(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes.Span);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string StripDoctype(string html)
    {
        var trimmed = html.TrimStart();
        if (!trimmed.StartsWith(DoctypePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var end = trimmed.IndexOf('>', StringComparison.Ordinal);
        return end < 0 ? trimmed : trimmed[(end + 1)..].TrimStart();
    }
}
