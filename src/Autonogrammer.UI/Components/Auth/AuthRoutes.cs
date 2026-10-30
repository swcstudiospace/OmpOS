using Microsoft.AspNetCore.Components;

namespace Autonogrammer.UI.Components.Auth;

/// <summary>The one sign-in route and the small helpers that read it from the address.</summary>
public static class AuthRoutes
{
    /// <summary>The route of the connect page, relative to the base address.</summary>
    public const string ConnectPath = "connect";

    private static readonly char[] QueryOrFragment = ['?', '#'];

    /// <summary>Gets the current address relative to the base address, with its query and fragment, for example <c>sessions/abc</c> or an empty string for the home page.</summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <returns>The relative path.</returns>
    public static string CurrentRelativePath(NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return navigation.ToBaseRelativePath(navigation.Uri);
    }

    /// <summary>Gets whether the current address is the connect page.</summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <returns>True when the path, ignoring query and fragment, is <see cref="ConnectPath"/>.</returns>
    public static bool IsConnectLocation(NavigationManager navigation)
    {
        var path = CurrentRelativePath(navigation);
        var cut = path.IndexOfAny(QueryOrFragment);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        return string.Equals(path.Trim('/'), ConnectPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Accepts a remembered path only when it is a plain path inside this app, so a sign-in can never send the user to another site.</summary>
    /// <param name="path">The remembered path, or null.</param>
    /// <returns>The path, or an empty string (the home page) when it is null, absolute, protocol-relative or the connect page itself.</returns>
    public static string SafeReturnPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        var firstColon = trimmed.IndexOf(':', StringComparison.Ordinal);
        var firstSlash = trimmed.IndexOf('/', StringComparison.Ordinal);
        var hasScheme = firstColon >= 0 && (firstSlash < 0 || firstColon < firstSlash);
        if (hasScheme || trimmed.StartsWith('/') || trimmed.StartsWith('\\') || trimmed.Contains('\\', StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var cut = trimmed.IndexOfAny(QueryOrFragment);
        var bare = cut >= 0 ? trimmed[..cut] : trimmed;
        return string.Equals(bare.Trim('/'), ConnectPath, StringComparison.OrdinalIgnoreCase) ? string.Empty : trimmed;
    }
}
