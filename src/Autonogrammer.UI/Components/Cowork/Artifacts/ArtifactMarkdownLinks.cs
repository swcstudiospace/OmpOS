using System.Net;
using System.Text.RegularExpressions;

namespace Autonogrammer.UI.Components.Cowork.Artifacts;

/// <summary>
/// The artifact link policy of an artifact Markdown view (71-02 amendment 2, the same rule as the desktop view). The shared pipeline
/// has already sanitized the HTML; this step turns every anchor into text. A relative, <c>artifact:</c> or <c>javascript:</c> target
/// becomes its text alone. An https target keeps its text and shows the full URL beside it, so the reader can act on it deliberately;
/// nothing here is an anchor, so no link navigates from the artifact view.
/// </summary>
public static partial class ArtifactMarkdownLinks
{
    private const string HttpsPrefix = "https://";

    /// <summary>Replaces every anchor of sanitized Markdown HTML with plain text under the artifact link policy.</summary>
    /// <param name="html">The sanitized HTML from <c>MarkdownRenderer.ToHtml</c>.</param>
    /// <returns>HTML with no anchor element.</returns>
    public static string Apply(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return AnchorPattern().Replace(html, Replace);
    }

    private static string Replace(Match match)
    {
        var text = match.Groups["text"].Value;
        var href = HrefPattern().Match(match.Groups["attributes"].Value);
        if (!href.Success)
        {
            // The shared renderer removes the href of a refused scheme, so that anchor arrives with no href at all.
            return text;
        }

        var target = WebUtility.HtmlDecode(Unquote(href.Groups["value"].Value)).Trim();
        if (target.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return text + " (" + WebUtility.HtmlEncode(target) + ")";
        }

        return text;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            ? value[1..^1]
            : value;

    [GeneratedRegex("<a\\b(?<attributes>[^>]*)>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 500)]
    private static partial Regex AnchorPattern();

    [GeneratedRegex("\\shref\\s*=\\s*(?<value>\"[^\"]*\"|'[^']*'|[^\\s>]+)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 500)]
    private static partial Regex HrefPattern();
}
