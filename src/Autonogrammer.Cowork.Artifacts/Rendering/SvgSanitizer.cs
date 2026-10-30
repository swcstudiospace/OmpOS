using System.Text;
using System.Xml;

namespace Autonogrammer.Cowork.Artifacts.Rendering;


/// <summary>In-house SVG allowlist walk. Output is written only by <see cref="XmlWriter"/>.</summary>
public static class SvgSanitizer
{
    /// <summary>One removal or rejection recorded while sanitising SVG.</summary>
    /// <param name="Code">Stable finding code.</param>
    /// <param name="Detail">A short reason that never echoes document text.</param>
    internal readonly record struct SvgFinding(string Code, string Detail);

    /// <summary>Sanitised SVG, or the original source when the document is rejected whole.</summary>
    internal sealed class SvgSanitizeResult
    {
        internal SvgSanitizeResult(bool accepted, string? document, string source, IReadOnlyList<SvgFinding> findings)
        {
            Accepted = accepted;
            Document = document;
            Source = source;
            Findings = findings;
        }

        /// <summary>Gets a value indicating whether an allowlisted document was produced.</summary>
        public bool Accepted { get; }

        /// <summary>Gets the XmlWriter serialisation when <see cref="Accepted"/> is true.</summary>
        public string? Document { get; }

        /// <summary>Gets the original source. Rejected documents render from this text.</summary>
        public string Source { get; }

        /// <summary>Gets every removal or rejection.</summary>
        public IReadOnlyList<SvgFinding> Findings { get; }

        /// <summary>Returns the sanitised document, or the source with findings listed when rejected.</summary>
        /// <returns>The renderable text.</returns>
        public string Render()
        {
            if (Accepted && Document is not null)
            {
                return Document;
            }

            var builder = new StringBuilder(Source);
            builder.Append("\n<!-- findings:");
            foreach (var finding in Findings)
            {
                builder.Append(' ').Append(finding.Code);
            }

            builder.Append(" -->");
            return builder.ToString();
        }
    }

    /// <summary>Maximum document characters read by the parser.</summary>
    public const int MaxCharactersInDocument = 262144;

    /// <summary>Maximum element depth.</summary>
    public const int MaxDepth = 64;

    /// <summary>Maximum elements.</summary>
    public const int MaxElements = 5000;

    /// <summary>Maximum characters in one attribute value.</summary>
    public const int MaxAttributeCharacters = 64 * 1024;

    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "title", "desc", "path", "rect", "circle", "ellipse", "line",
        "polyline", "polygon", "text", "tspan", "linearGradient", "radialGradient", "stop",
        "clipPath", "mask", "pattern", "symbol", "use", "marker", "style", "image", "filter",
        "feGaussianBlur", "feOffset", "feBlend", "feColorMatrix", "feComposite", "feFlood",
    };

    private static readonly HashSet<string> TextParents = new(StringComparer.Ordinal)
    {
        "title", "desc", "text", "tspan", "style",
    };

    /// <summary>Sanitises UTF-8 SVG bytes.</summary>
    /// <param name="utf8">The document bytes.</param>
    /// <returns>An accepted document or a whole-document rejection.</returns>
    internal static SvgSanitizeResult Sanitize(ReadOnlySpan<byte> utf8)
    {
        var source = Encoding.UTF8.GetString(utf8);
        return Sanitize(source);
    }

    /// <summary>Sanitises an SVG document. DOCTYPE and entity expansion reject the document whole.</summary>
    /// <param name="xml">The document text.</param>
    /// <returns>An accepted document or a whole-document rejection.</returns>
    internal static SvgSanitizeResult Sanitize(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var findings = new List<SvgFinding>();
        try
        {
            var root = Walk(xml, findings, out var rejected);
            if (rejected || root is null)
            {
                return new SvgSanitizeResult(false, null, xml, findings);
            }

            return new SvgSanitizeResult(true, Write(root), xml, findings);
        }
        catch (XmlException)
        {
            var code = xml.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ? "doctype" : "entity";
            findings.Add(new SvgFinding(code, "The document was rejected without expansion."));
            return new SvgSanitizeResult(false, null, xml, findings);
        }
    }

    private static SvgNode? Walk(string xml, List<SvgFinding> findings, out bool rejected)
    {
        rejected = false;
        var settings = CreateSettings();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        using var reader = XmlReader.Create(stream, settings);
        SvgNode? root = null;
        var stack = new Stack<SvgNode>();
        var elements = 0;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.DocumentType)
            {
                findings.Add(new SvgFinding("doctype", "DOCTYPE is rejected whole."));
                rejected = true;
                return null;
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (!AcceptElement(reader, ref elements, stack, ref root, findings))
                {
                    rejected = true;
                    return null;
                }

                continue;
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (stack.Count > 0 && string.Equals(stack.Peek().Name, reader.LocalName, StringComparison.Ordinal))
                {
                    stack.Pop();
                }
            }
            else if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
            {
                AcceptText(reader, stack, findings);
            }
        }

        if (root is null)
        {
            findings.Add(new SvgFinding("not_svg", "No svg root was found."));
            rejected = true;
        }

        Prune(root);
        return root;
    }
    private static XmlReaderSettings CreateSettings()
    {
        var settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Prohibit;
        settings.XmlResolver = null;
        settings.MaxCharactersInDocument = MaxCharactersInDocument;
        settings.MaxCharactersFromEntities = 0;
        settings.IgnoreComments = true;
        settings.IgnoreProcessingInstructions = true;
        settings.CloseInput = true;
        return settings;
    }
    private static void AcceptText(XmlReader reader, Stack<SvgNode> stack, List<SvgFinding> findings)
    {
        if (stack.Count == 0 || !TextParents.Contains(stack.Peek().Name))
        {
            return;
        }

        var parent = stack.Peek();
        parent.Text = string.Concat(parent.Text, reader.Value);
        if (string.Equals(parent.Name, "style", StringComparison.Ordinal) && !CssCheck.IsConservative(parent.Text))
        {
            findings.Add(new SvgFinding("css", "A style element was dropped."));
            parent.Drop = true;
        }
    }

    private static bool AcceptElement(XmlReader reader, ref int elements, Stack<SvgNode> stack, ref SvgNode? root, List<SvgFinding> findings)
    {
        elements++;
        if (elements > MaxElements || reader.Depth > MaxDepth)
        {
            findings.Add(new SvgFinding(elements > MaxElements ? "elements" : "depth", "A parser cap was exceeded."));
            return false;
        }

        var local = reader.LocalName;
        var empty = reader.IsEmptyElement;
        if (!Elements.Contains(local))
        {
            findings.Add(new SvgFinding(FindingCode(local), "An element was removed."));
            SkipElement(reader, empty);
            return true;
        }

        var node = new SvgNode(local);
        if (!CopyAttributes(reader, node, findings))
        {
            return false;
        }

        if (stack.Count == 0 && !string.Equals(local, "svg", StringComparison.Ordinal))
        {
            findings.Add(new SvgFinding("not_svg", "The root is not svg."));
            return false;
        }

        if (stack.Count == 0)
        {
            root = node;
        }
        else
        {
            stack.Peek().Children.Add(node);
        }

        if (!empty)
        {
            stack.Push(node);
        }

        return true;
    }



    private static void Prune(SvgNode? node)
    {
        if (node is null)
        {
            return;
        }

        node.Children.RemoveAll(child => child.Drop);
        foreach (var child in node.Children)
        {
            Prune(child);
        }
    }

    private static bool CopyAttributes(XmlReader reader, SvgNode node, List<SvgFinding> findings)
    {
        if (!reader.HasAttributes)
        {
            return true;
        }

        for (var index = 0; index < reader.AttributeCount; index++)
        {
            reader.MoveToAttribute(index);
            var name = reader.LocalName;
            var value = reader.Value;
            if (value.Length > MaxAttributeCharacters)
            {
                findings.Add(new SvgFinding("attribute", "An attribute exceeded the cap."));
                return false;
            }

            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new SvgFinding("event_handler", "An event attribute was removed."));
                continue;
            }

            if (name.Equals("style", StringComparison.Ordinal))
            {
                if (!CssCheck.IsConservative(value))
                {
                    findings.Add(new SvgFinding("css", "A style attribute was dropped."));
                    continue;
                }
            }

            if (name.Equals("href", StringComparison.Ordinal) && !IsAllowedHref(node.Name, value))
            {
                findings.Add(new SvgFinding("external_ref", "An href was removed."));
                continue;
            }

            if (name.Equals("xmlns", StringComparison.Ordinal) || name.Equals("space", StringComparison.Ordinal))
            {
                continue;
            }

            node.Attributes.Add((name, value));
        }

        reader.MoveToElement();
        return true;
    }

    private static bool IsAllowedHref(string element, string value)
    {
        if (value.StartsWith('#'))
        {
            return value.Length > 1 && !value.Contains(':', StringComparison.Ordinal) && !value.Contains("//", StringComparison.Ordinal);
        }

        if (!string.Equals(element, "image", StringComparison.Ordinal))
        {
            return false;
        }

        return value.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/jpeg", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/gif", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/webp", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindingCode(string local) => local switch
    {
        "script" => "script",
        "foreignObject" => "foreign_object",
        _ => "element",
    };

    private static void SkipElement(XmlReader reader, bool empty)
    {
        if (empty)
        {
            return;
        }

        var depth = reader.Depth;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
            {
                return;
            }
        }
    }

    private static string Write(SvgNode root)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            CloseOutput = true,
            Encoding = Encoding.UTF8,
        };
        var buffer = new StringBuilder();
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            WriteNode(writer, root);
        }

        return buffer.ToString();
    }

    private static void WriteNode(XmlWriter writer, SvgNode node)
    {
        writer.WriteStartElement(node.Name, "http://www.w3.org/2000/svg");
        foreach (var (name, value) in node.Attributes)
        {
            writer.WriteAttributeString(name, value);
        }

        if (node.Text is not null)
        {
            writer.WriteString(node.Text);
        }

        foreach (var child in node.Children)
        {
            WriteNode(writer, child);
        }

        writer.WriteEndElement();
    }

    private sealed class SvgNode(string name)
    {
        public string Name { get; } = name;

        public List<(string Name, string Value)> Attributes { get; } = [];

        public List<SvgNode> Children { get; } = [];

        public string? Text { get; set; }

        public bool Drop { get; set; }
    }
}
