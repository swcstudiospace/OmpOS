using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Types through Input.insertText. The text is never returned.</summary>
public static class BrowserTypeTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_type";

    /// <summary>Inserts text. The result does not echo it.</summary>
    /// <param name="session">The page.</param>
    /// <param name="text">The text. It is not logged.</param>
    /// <param name="submit">Whether to press Enter.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public static async Task TypeAsync(ICDPSession session, string text, bool submit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Input.insertText", new { text }, waitForCallback: true, options: null).ConfigureAwait(false);
        if (submit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = await session.SendAsync("Input.dispatchKeyEvent", new { type = "keyDown", key = "Enter" }, waitForCallback: true, options: null).ConfigureAwait(false);
        }
    }
}
