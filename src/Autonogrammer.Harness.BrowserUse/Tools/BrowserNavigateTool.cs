using System.Text.Json;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Navigates and reports a redirect for stage 2. It does not log the URL query.</summary>
public static class BrowserNavigateTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_navigate";

    /// <summary>Reads the requested URL without touching the page.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="url">The URL.</param>
    /// <returns>True when the argument is an absolute URL.</returns>
    public static bool TryReadUrl(JsonElement arguments, out Uri? url)
    {
        url = null;
        if (!arguments.TryGetProperty("url", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return Uri.TryCreate(value.GetString(), UriKind.Absolute, out url);
    }

    /// <summary>Sends Page.navigate and returns the host-observed final URL.</summary>
    /// <param name="session">The page.</param>
    /// <param name="url">The requested URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The final URL.</returns>
    public static async Task<Uri> GoAsync(ICDPSession session, Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(url);
        cancellationToken.ThrowIfCancellationRequested();
        var final = await session.SendAsync("Page.navigate", new { url = url.AbsoluteUri }, waitForCallback: true, options: null).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (final is { } element && element.TryGetProperty("url", out var observed) && Uri.TryCreate(observed.GetString(), UriKind.Absolute, out var parsed))
        {
            return parsed;
        }

        return url;
    }
}
