namespace Autonogrammer.Cowork.Artifacts.Rendering;

/// <summary>Wraps an Html artifact so the policy meta is first and dialogs are no-ops.</summary>
public static class HtmlDocumentWrapper
{
    /// <summary>Host prefix. It does not subscribe to messages and does not call the host bridge.</summary>
    public const string DialogNoOpPrefix =
        "<script>window.alert=function(){};window.confirm=function(){return false;};window.prompt=function(){return null;};</script>";

    /// <summary>Places the policy meta first, drops a leading DOCTYPE, and prefixes the dialog no-op script.</summary>
    /// <param name="html">The untrusted document.</param>
    /// <param name="scripts">Whether the Html policy allows inline script.</param>
    /// <returns>The wrapped document.</returns>
    public static string Wrap(string html, bool scripts)
    {
        ArgumentNullException.ThrowIfNull(html);
        var body = StripDoctype(html);
        var policy = ArtifactDocumentPolicy.ForHtml(scripts);
        return string.Concat(ArtifactDocumentPolicy.MetaElement(policy), DialogNoOpPrefix, body);
    }

    private static string StripDoctype(string html)
    {
        var trimmed = html.TrimStart();
        if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
        {
            var end = trimmed.IndexOf('>', StringComparison.Ordinal);
            if (end >= 0)
            {
                return trimmed[(end + 1)..].TrimStart();
            }
        }

        return trimmed;
    }
}
