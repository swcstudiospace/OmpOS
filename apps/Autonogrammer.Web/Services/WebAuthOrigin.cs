namespace Autonogrammer.Web.Services;

/// <summary>The built auth origin (<c>Auth:Origin</c>) and the rule that refuses a mismatched project URL.</summary>
internal static class WebAuthOrigin
{
    /// <summary>The configuration key of the built auth origin. Empty means unset.</summary>
    internal const string ConfigKey = "Auth:Origin";

    /// <summary>The cooldown the web head uses after a 429. <c>Retry-After</c> is unreadable cross-origin, so it is never read.</summary>
    internal static readonly TimeSpan RateLimitCooldown = TimeSpan.FromSeconds(60);

    /// <summary>Gets whether a client block must be refused before any sign-in call.</summary>
    /// <param name="config">The loaded configuration.</param>
    /// <param name="builtOrigin">The origin baked into the web head, or null when unset.</param>
    /// <returns>True when a client block is served and the built origin is missing or differs.</returns>
    internal static bool Refuses(UI.Components.Auth.AuthConfig config, string? builtOrigin)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!config.HasClientBlock)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(builtOrigin) || config.ProjectUrl is null)
        {
            return true;
        }

        return !OriginsMatch(builtOrigin, config.ProjectUrl);
    }

    /// <summary>Compares scheme, host and port. The built origin must be https.</summary>
    /// <param name="builtOrigin">The built origin.</param>
    /// <param name="projectUrl">The project URL from the API.</param>
    /// <returns>True when they are the same https origin.</returns>
    internal static bool OriginsMatch(string builtOrigin, Uri projectUrl)
    {
        if (!Uri.TryCreate(builtOrigin.Trim(), UriKind.Absolute, out var origin))
        {
            return false;
        }

        return string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin.Scheme, projectUrl.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin.IdnHost, projectUrl.IdnHost, StringComparison.OrdinalIgnoreCase)
            && origin.Port == projectUrl.Port;
    }
}
