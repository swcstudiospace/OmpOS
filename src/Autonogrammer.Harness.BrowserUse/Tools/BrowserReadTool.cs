using System.Text.Json;
using Autonogrammer.Harness.BrowserUse.Content;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Reads the view tree, visible text or console through the extractor.</summary>
public static class BrowserReadTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_read";

    /// <summary>Reads and masks sensitive values. The returned text is untrusted.</summary>
    /// <param name="session">The page.</param>
    /// <param name="view">tree, text or console.</param>
    /// <param name="marker">The marker.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The enveloped text.</returns>
    public static async Task<string> ReadAsync(ICDPSession session, string view, PageMarker marker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(marker);
        cancellationToken.ThrowIfCancellationRequested();
        var method = string.Equals(view, "console", StringComparison.Ordinal)
            ? "Runtime.evaluate"
            : string.Equals(view, "tree", StringComparison.Ordinal) ? "Accessibility.getFullAXTree" : "DOMSnapshot.captureSnapshot";
        var payload = await session.SendAsync(method, new { }, waitForCallback: true, options: null).ConfigureAwait(false);
        return marker.Wrap(PageContentExtractor.Extract(ReadNodes(payload)));
    }

    private static List<PageExtractNode> ReadNodes(JsonElement? payload)
    {
        var nodes = new List<PageExtractNode>();
        if (payload is not { } element || !element.TryGetProperty("nodes", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return nodes;
        }

        foreach (var node in array.EnumerateArray())
        {
            var text = node.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
            var visible = !node.TryGetProperty("visible", out var visibleElement) || visibleElement.ValueKind != JsonValueKind.False;
            var kind = node.TryGetProperty("kind", out var kindElement) && Enum.TryParse<PageTextKind>(kindElement.GetString(), true, out var parsed)
                ? parsed
                : PageTextKind.Text;
            var sensitive = node.TryGetProperty("sensitive", out var sensitiveElement) && sensitiveElement.ValueKind == JsonValueKind.True;
            nodes.Add(new PageExtractNode(text, visible, kind, sensitive));
        }

        return nodes;
    }
}
