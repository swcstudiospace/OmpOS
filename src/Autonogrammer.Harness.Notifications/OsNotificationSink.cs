#pragma warning disable MA0048 // The desktop enum belongs with the sink that selects it.
namespace Autonogrammer.Harness.Notifications;

/// <summary>Which desktop the operating-system sink is talking to. Windows is in-app only.</summary>
public enum NotificationDesktop
{
    /// <summary>Linux, via <c>notify-send</c>.</summary>
    Linux = 0,

    /// <summary>macOS, via <c>osascript</c>.</summary>
    MacOs = 1,

    /// <summary>Windows. No operating-system notification is attempted.</summary>
    Windows = 2,
}

/// <summary>
/// Delivers a title and an optional summary through <c>notify-send</c> or <c>osascript</c>, by argument vector only.
/// A missing or failed tool returns a named unavailability state. Windows returns
/// <see cref="WindowsNotSupported"/> without starting a process. This type does not draw a notification itself.
/// </summary>
public sealed class OsNotificationSink
{
    /// <summary><c>notify-send</c> is not installed.</summary>
    public const string NotifySendMissing = "NotificationOsUnavailable(notify_send_missing)";

    /// <summary><c>osascript</c> failed.</summary>
    public const string OsascriptFailed = "NotificationOsUnavailable(osascript_failed)";

    /// <summary>Windows has no operating-system notification in this product.</summary>
    public const string WindowsNotSupported = "NotificationOsUnavailable(windows_not_supported)";

    private readonly INotificationProcessRunner _runner;
    private readonly NotificationDesktop _desktop;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new instance of the <see cref="OsNotificationSink"/> class.</summary>
    /// <param name="runner">Starts the tool by argument vector.</param>
    /// <param name="desktop">Which desktop to target. Tests pass this so the host operating system is not consulted.</param>
    /// <param name="timeout">How long to wait for the tool. Defaults to five seconds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public OsNotificationSink(INotificationProcessRunner runner, NotificationDesktop desktop, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        if (timeout is { } given && given <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive.");
        }

        _runner = runner;
        _desktop = desktop;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Gets the desktop this host would target. Anything that is not Windows or macOS is treated as Linux.</summary>
    public static NotificationDesktop CurrentDesktop =>
        OperatingSystem.IsWindows() ? NotificationDesktop.Windows
        : OperatingSystem.IsMacOS() ? NotificationDesktop.MacOs
        : NotificationDesktop.Linux;

    /// <summary>Delivers one notification, or returns the named unavailability state.</summary>
    /// <param name="title">The title.</param>
    /// <param name="summary">The summary, or null when the surface is title only.</param>
    /// <param name="cancellationToken">Cancels the delivery. A timeout is a separate cancellation and maps to the platform's failure state.</param>
    /// <returns>Null when the tool exited 0. Otherwise a <c>NotificationOsUnavailable</c> state.</returns>
    public async Task<string?> DeliverAsync(string title, string? summary, CancellationToken cancellationToken)
    {
        if (_desktop == NotificationDesktop.Windows)
        {
            return WindowsNotSupported;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            return _desktop == NotificationDesktop.MacOs
                ? await DeliverMacAsync(title, summary, timeout.Token).ConfigureAwait(false)
                : await DeliverLinuxAsync(title, summary, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return _desktop == NotificationDesktop.MacOs ? OsascriptFailed : NotifySendMissing;
        }
    }

    private async Task<string?> DeliverLinuxAsync(string title, string? summary, CancellationToken cancellationToken)
    {
        var command = NotifySendCommand.Create(title, summary);
        var exit = await _runner.RunAsync(NotifySendCommand.FileName, command.Arguments, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        return exit is 0 ? null : NotifySendMissing;
    }

    private async Task<string?> DeliverMacAsync(string title, string? summary, CancellationToken cancellationToken)
    {
        var command = OsascriptCommand.Create(title, summary);
        var exit = await _runner.RunAsync(OsascriptCommand.FileName, command.Arguments, OsascriptCommand.StandardInput, cancellationToken).ConfigureAwait(false);
        return exit is 0 ? null : OsascriptFailed;
    }
}
