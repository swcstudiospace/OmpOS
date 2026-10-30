using System.Text;

namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Builds an <c>osascript</c> argument vector. The AppleScript source is a fixed constant. Notification text is passed
/// only as <c>on run argv</c> parameters and is never spliced into the source. Control characters are stripped and each
/// argument is capped at 200 characters.
/// </summary>
public sealed class OsascriptCommand
{
    /// <summary>
    /// The AppleScript source, byte for byte. It reads the title and the summary from <c>argv</c> and contains no notification text.
    /// </summary>
    public const string Source = "on run argv\ndisplay notification (item 2 of argv) with title (item 1 of argv)\nend run\n";

    private OsascriptCommand(IReadOnlyList<string> arguments) => Arguments = arguments;

    /// <summary>Gets the executable name.</summary>
    public static string FileName => "osascript";

    /// <summary>
    /// Gets the argument vector: the program file <c>-</c> (read the source from standard input), then the title, then the summary.
    /// The text is not part of the source.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Gets the fixed source as UTF-8 bytes for standard input. It does not contain notification text.</summary>
    public static ReadOnlyMemory<byte> StandardInput { get; } = Encoding.UTF8.GetBytes(Source);

    /// <summary>Builds the argument vector. The summary is an empty argument when the surface is title only, so the source stays fixed.</summary>
    /// <param name="title">The title.</param>
    /// <param name="summary">The summary, or null when the surface is title only.</param>
    /// <returns>The command.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty after control characters are removed.</exception>
    public static OsascriptCommand Create(string title, string? summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var safeTitle = NotificationText.Sanitize(title);
        if (safeTitle.Length == 0)
        {
            throw new ArgumentException("The title has no characters left after control characters are removed.", nameof(title));
        }

        var safeSummary = string.IsNullOrEmpty(summary) ? string.Empty : NotificationText.Sanitize(summary);
        return new OsascriptCommand(["-", safeTitle, safeSummary]);
    }
}
