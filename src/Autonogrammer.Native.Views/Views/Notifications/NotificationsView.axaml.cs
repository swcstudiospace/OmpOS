using Autonogrammer.Presentation.Notifications;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Notifications;

/// <summary>
/// The in-app notification centre: a toast, a newest-first list, a polite live region and the Show content toggle.
/// It binds a <see cref="NotificationCentreModel"/> and does not reference Harness.Notifications.
/// </summary>
internal partial class NotificationsView : UserControl
{
    /// <summary>Creates a view with no centre.</summary>
    public NotificationsView()
    {
        InitializeComponent();
    }

    /// <summary>Creates a view bound to <paramref name="model"/>.</summary>
    /// <param name="model">The centre.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public NotificationsView(NotificationCentreModel model)
        : this()
    {
        ArgumentNullException.ThrowIfNull(model);
        DataContext = model;
    }

    /// <summary>Gets the Show content toggle.</summary>
    internal CheckBox ShowContent => ShowContentBox;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Toast => ToastText;

    /// <summary>Gets the list.</summary>
    internal ItemsControl List => NotificationList;
}
