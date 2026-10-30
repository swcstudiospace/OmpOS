using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Composition.Notifications;

/// <summary>
/// In-app centre bridge for run-finished, failed and approval-needed signals. NotificationRequest and
/// INotificationSink live in Harness.Notifications. Presentation never references them: this bridge sits in
/// Native and Native.Views, so Presentation has no project reference to Harness.Notifications.
/// Scheduled-task kinds (finished, failed, missed, waiting) have no producer: phase 75-03 events were not
/// present as a notification source when this plan ran, so those kinds are left without a producer.
/// </summary>
public sealed class NotificationBridge
{
    private readonly List<NotificationCentreItem> _items = [];
    private readonly bool _windowFocused;

    /// <summary>Creates the bridge.</summary>
    /// <param name="windowFocused">The main window's activation state. A focused window suppresses OS delivery.</param>
    public NotificationBridge(bool windowFocused = false)
    {
        _windowFocused = windowFocused;
    }

    /// <summary>Gets whether the window was focused when the bridge was created. Focus suppresses OS delivery.</summary>
    public bool WindowFocused => _windowFocused;

    /// <summary>Gets the in-app items, title only by default.</summary>
    public IReadOnlyList<NotificationCentreItem> Items => _items;

    /// <summary>Gets whether an OS sink would be suppressed because the window is focused.</summary>
    public bool SuppressOsDelivery => _windowFocused;

    /// <summary>Maps a harness event into the centre. Events that are not run or approval signals are ignored.</summary>
    /// <param name="evt">The event from the tee.</param>
    /// <param name="sessionId">The session the event belongs to.</param>
    public void Observe(HarnessEvent evt, string? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(evt);
        NotificationCentreItem? item = evt switch
        {
            CompletedEvent completed => new("run-finished", completed.Reason, sessionId, ShowContent: false),
            ErrorEvent error => new("approval-needed", error.Message, sessionId, ShowContent: false),
            PermissionRequestEvent permission => new("approval-needed", permission.Name, sessionId, ShowContent: false),
            _ => null,
        };
        if (item is not null)
        {
            _items.Add(item);
        }
    }
}

/// <summary>One in-app centre item. OS text stays title-only unless <paramref name="ShowContent"/> is set.</summary>
/// <param name="Kind">The kind name. Scheduled-task kinds are not produced here.</param>
/// <param name="Title">The title. This is what an OS sink would show by default.</param>
/// <param name="SessionId">The session, or null.</param>
/// <param name="ShowContent">True only when the user asked to show content. The default is title-only.</param>
public sealed record NotificationCentreItem(string Kind, string Title, string? SessionId, bool ShowContent);
