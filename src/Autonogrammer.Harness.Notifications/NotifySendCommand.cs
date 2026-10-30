namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Builds a <c>notify-send</c> argument vector. Notification text is one argument each. No shell string is built.
/// Control characters are stripped and each argument is capped at 200 characters.
/// </summary>
public sealed class NotifySendCommand
{
    private NotifySendCommand(IReadOnlyList<string> arguments) => Arguments = arguments;

    /// <summary>Gets the executable name. It is not combined with the arguments into a command line.</summary>
    public static string FileName => "notify-send";

    /// <summary>Gets the argument vector, not including <see cref="FileName"/>.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>Builds the argument vector for a title and an optional summary.</summary>
    /// <param name="title">The title.</param>
    /// <param name="summary">The summary, or null when the surface is title only.</param>
    /// <returns>The command.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty.</exception>
    public static NotifySendCommand Create(string title, string? summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var safeTitle = NotificationText.Sanitize(title);
        if (safeTitle.Length == 0)
        {
            throw new ArgumentException("The title has no characters left after control characters are removed.", nameof(title));
        }

        if (string.IsNullOrEmpty(summary))
        {
            return new NotifySendCommand([safeTitle]);
        }

        var safeSummary = NotificationText.Sanitize(summary);
        return safeSummary.Length == 0
            ? new NotifySendCommand([safeTitle])
            : new NotifySendCommand([safeTitle, safeSummary]);
    }
}
