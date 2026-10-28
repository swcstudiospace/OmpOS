namespace OmpOS.Presentation.Mentions;

/// <summary>Picks the MIME type of a mentioned file from its extension.</summary>
internal static class MentionMime
{
    /// <summary>Returns the MIME type for a path.</summary>
    /// <param name="path">The file path or name.</param>
    /// <returns><c>text/markdown</c>, <c>application/json</c>, <c>text/x-diff</c> or <c>text/plain</c>.</returns>
    internal static string ForPath(string path)
    {
        var dot = path.LastIndexOf('.');
        var extension = dot >= 0 ? path[dot..] : string.Empty;
        return extension.ToUpperInvariant() switch
        {
            ".MD" or ".MDX" or ".MARKDOWN" => "text/markdown",
            ".JSON" => "application/json",
            ".DIFF" or ".PATCH" => "text/x-diff",
            ".HTML" or ".HTM" => "text/html",
            ".XML" or ".AXAML" or ".CSPROJ" or ".SLNX" => "application/xml",
            _ => "text/plain",
        };
    }
}
