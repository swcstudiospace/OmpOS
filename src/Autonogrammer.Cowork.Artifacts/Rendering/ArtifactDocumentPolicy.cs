namespace Autonogrammer.Cowork.Artifacts.Rendering;


/// <summary>
/// The only place a content-security policy is written. Desktop meta omits frame-ancestors and sandbox;
/// those directives are ignored in a meta element and are carried by <see cref="RenderResponseHeaders"/>.
/// </summary>
public static class ArtifactDocumentPolicy
{

    /// <summary>Html policy with scripts on, exactly the ADR-0033 D7.2 text.</summary>
    public const string HtmlScriptsOn =
        "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; manifest-src 'none'";

    /// <summary>Html policy with scripts off.</summary>
    public const string HtmlScriptsOff =
        "default-src 'none'; script-src 'none'; style-src 'unsafe-inline'; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; frame-src 'none'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; manifest-src 'none'";

    /// <summary>Svg and Image document policy.</summary>
    public const string SvgOrImage = "default-src 'none'; img-src data:";

    /// <summary>Permissions-Policy denying the API baseline features plus artifact extras.</summary>
    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=(), clipboard-read=(), clipboard-write=(), display-capture=(), fullscreen=(), midi=(), screen-wake-lock=(), serial=(), xr-spatial-tracking=()";

    /// <summary>Returns the Html document policy. Approved origins never reach script-src.</summary>
    /// <param name="scripts">Whether inline script is allowed.</param>
    /// <param name="approvedOrigins">Optional https origins added only to connect, image, font and media sources.</param>
    /// <returns>The policy string.</returns>
    public static string ForHtml(bool scripts, IReadOnlyList<string>? approvedOrigins = null)
    {
        var policy = scripts ? HtmlScriptsOn : HtmlScriptsOff;
        return ApplyOrigins(policy, approvedOrigins, includeBlob: true);
    }

    /// <summary>Returns the Svg or Image document policy.</summary>
    /// <param name="approvedOrigins">Optional https origins added only to connect, image, font and media sources.</param>
    /// <returns>The policy string.</returns>
    public static string ForSvgOrImage(IReadOnlyList<string>? approvedOrigins = null) =>
        ApplyOrigins(SvgOrImage, approvedOrigins, includeBlob: false);

    /// <summary>Builds the meta element that must be placed first. It does not include frame-ancestors or sandbox.</summary>
    /// <param name="policy">A policy string produced by this type.</param>
    /// <returns>The meta element.</returns>
    public static string MetaElement(string policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return $"<meta http-equiv=\"Content-Security-Policy\" content=\"{policy}\">";
    }

    /// <summary>
    /// Response-header form consumed by the render route. The content-security policy is the Html scripts-on
    /// policy, then sandbox allow-scripts, then frame-ancestors.
    /// </summary>
    /// <param name="frameAncestors">Origins for frame-ancestors, or empty for 'none'.</param>
    /// <returns>Ordered name and value pairs. Never includes X-Frame-Options.</returns>
    public static IReadOnlyList<(string Name, string Value)> RenderResponseHeaders(IReadOnlyList<string> frameAncestors)
    {
        ArgumentNullException.ThrowIfNull(frameAncestors);
        var ancestors = frameAncestors.Count == 0 ? "'none'" : string.Join(' ', frameAncestors);
        var policy = string.Concat(HtmlScriptsOn, "; sandbox allow-scripts; frame-ancestors ", ancestors);
        return
        [
            ("Content-Security-Policy", policy),
            ("Referrer-Policy", "no-referrer"),
            ("X-Content-Type-Options", "nosniff"),
            ("Cache-Control", "no-store"),
            ("Permissions-Policy", PermissionsPolicy),
        ];
    }

    private static string ApplyOrigins(string policy, IReadOnlyList<string>? approvedOrigins, bool includeBlob)
    {
        if (approvedOrigins is null || approvedOrigins.Count == 0)
        {
            return policy;
        }

        var joined = string.Join(' ', approvedOrigins);
        if (includeBlob)
        {
            return policy
                .Replace("connect-src 'none'", "connect-src " + joined, StringComparison.Ordinal)
                .Replace("img-src data: blob:", "img-src data: blob: " + joined, StringComparison.Ordinal)
                .Replace("font-src data:", "font-src data: " + joined, StringComparison.Ordinal)
                .Replace("media-src data: blob:", "media-src data: blob: " + joined, StringComparison.Ordinal);
        }

        var image = policy.Replace("img-src data:", "img-src data: " + joined, StringComparison.Ordinal);
        return string.Concat(image, "; connect-src ", joined, "; font-src ", joined, "; media-src ", joined);
    }
}
