using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Autonogrammer.Presentation.Cowork.Artifacts;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// Builds the host document a surface loads for one version (D-23). The policy text comes only from
/// <see cref="ArtifactDocumentPolicy"/>; the meta element is always first. Html keeps scripts on, Svg is sanitised first and
/// its policy allows no script, and Mermaid uses the bundle from <see cref="IMermaidAssetProvider"/>, so a missing bundle yields
/// <see cref="ArtifactSurfaceReason.MermaidAssetMissing"/> and the caller shows the source.
/// </summary>
/// <remarks>
/// The Avalonia WebKitGTK adapter installs <c>invokeCSharpAction</c> and a <c>window.webkit.messageHandlers</c> entry in every
/// page, whether or not the app subscribes to messages, and the public API has no switch for it. The host document therefore
/// removes both names as its first script, before any artifact script runs. The live probe checks the result.
/// </remarks>
public static class ArtifactDocumentBuilder
{
    /// <summary>The first script of every script-enabled host document. It removes the adapter's message names from the page.</summary>
    public const string BridgeRemoval =
        "<script>(function(){function drop(o,k){try{delete o[k];}catch(e){}if(typeof o[k]!=='undefined'){try{o[k]=undefined;}catch(e){}}}drop(window,'invokeCSharpAction');if(window.webkit){drop(window.webkit,'messageHandlers');}drop(window,'webkit');})();</script>";

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

    private static ArtifactDocumentResult BuildHtml(ArtifactContent content, IReadOnlyList<string> approvedOrigins)
    {
        var html = Decode(content.Bytes);
        if (html is null)
        {
            return ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused);
        }

        var policy = ArtifactDocumentPolicy.ForHtml(scripts: true, approvedOrigins);
        return ArtifactDocumentResult.Ready(
            WithBridgeRemoval(ArtifactDocumentPolicy.MetaElement(policy) + HtmlDocumentWrapper.DialogNoOpPrefix + StripDoctype(html)));
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
            ? ArtifactDocumentResult.Ready(WithBridgeRemoval(document.Html))
            : ArtifactDocumentResult.Refused(ArtifactSurfaceReason.MermaidAssetMissing);
    }

    /// <summary>Places the bridge removal directly after the policy meta element, which is the first element of every document.</summary>
    /// <param name="document">A document that starts with its policy meta element.</param>
    /// <returns>The document with the removal script after the meta element.</returns>
    private static string WithBridgeRemoval(string document)
    {
        var metaEnd = document.IndexOf('>', StringComparison.Ordinal) + 1;
        return document[..metaEnd] + BridgeRemoval + document[metaEnd..];
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
