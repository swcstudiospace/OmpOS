namespace Autonogrammer.Cowork.Artifacts.Rendering;

/// <summary>Conservative CSS gate. A hit drops the whole style block; nothing is rewritten.</summary>
public static class CssCheck
{
    /// <summary>Returns whether the style text has no import, external url, or other non-conservative construct.</summary>
    /// <param name="css">The style element or attribute text.</param>
    /// <returns>True when the block may be kept unchanged.</returns>
    public static bool IsConservative(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        if (css.Contains("@import", StringComparison.OrdinalIgnoreCase)
            || css.Contains("url(", StringComparison.OrdinalIgnoreCase)
            || css.Contains("expression(", StringComparison.OrdinalIgnoreCase)
            || css.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
            || css.Contains("-moz-binding", StringComparison.OrdinalIgnoreCase)
            || css.Contains("behavior:", StringComparison.OrdinalIgnoreCase)
            || css.Contains("@charset", StringComparison.OrdinalIgnoreCase)
            || css.Contains("@font-face", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
