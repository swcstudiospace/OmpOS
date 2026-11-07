using System.Net;
using System.Text;

namespace Autonogrammer.Cowork.Artifacts.Rendering;


/// <summary>Builds the Mermaid host document with strict keys. Diagram source is escaped text.</summary>
public static class MermaidDocumentBuilder
{
    /// <summary>A Mermaid host document, or an unavailable result that still shows source.</summary>
    internal sealed class MermaidDocument
    {
        private MermaidDocument(bool available, string? html, string source, string? reason)
        {
            Available = available;
            Html = html;
            Source = source;
            Reason = reason;
        }

        /// <summary>Gets a value indicating whether the host document was built.</summary>
        public bool Available { get; }

        /// <summary>Gets the host document when <see cref="Available"/> is true.</summary>
        public string? Html { get; }

        /// <summary>Gets the diagram source, shown when the bundle is missing.</summary>
        public string Source { get; }

        /// <summary>Gets <c>MermaidAssetMissing</c> when the bundle is absent.</summary>
        public string? Reason { get; }

        /// <summary>Builds an unavailable result that shows source.</summary>
        /// <param name="source">The diagram source.</param>
        /// <returns>The unavailable document.</returns>
        public static MermaidDocument Unavailable(string source) =>
            new(false, null, source, "MermaidAssetMissing");

        internal static MermaidDocument Ready(string html, string source) => new(true, html, source, null);
    }

    /// <summary>Host initialiser. Keys are fixed; the diagram source is not interpolated here.</summary>
    public const string HostScript =
        "mermaid.initialize({securityLevel:'strict',startOnLoad:false,maxTextSize:50000,maxEdges:500,htmlLabels:false});";

    /// <summary>Builds a document, or <see cref="MermaidDocument.Unavailable(string)"/> when the asset is missing.</summary>
    /// <param name="diagram">The diagram source.</param>
    /// <param name="assets">The bundle seam.</param>
    /// <returns>The host document or the source-view fallback.</returns>
    internal static MermaidDocument Build(string diagram, IMermaidAssetProvider assets)
    {
        ArgumentNullException.ThrowIfNull(diagram);
        ArgumentNullException.ThrowIfNull(assets);
        var script = assets.GetScript();
        if (string.IsNullOrEmpty(script))
        {
            return MermaidDocument.Unavailable(diagram);
        }

        var builder = new StringBuilder();
        builder.Append(ArtifactDocumentPolicy.MetaElement(ArtifactDocumentPolicy.ForHtml(scripts: true)));
        builder.Append("<pre class=\"mermaid\">");
        builder.Append(WebUtility.HtmlEncode(diagram));
        builder.Append("</pre><script>");
        builder.Append(script);
        builder.Append("</script><script>");
        builder.Append(HostScript);
        builder.Append("</script>");
        return MermaidDocument.Ready(builder.ToString(), diagram);
    }
}
