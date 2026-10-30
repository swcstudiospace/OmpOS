using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Notifications;

/// <summary>
/// The in-app notification centre: a toast plus a list, newest first, and a live-region announcement. Title-only items
/// keep their summary but do not show it until <see cref="ShowContent"/> is on. This type uses Presentation types only.
/// </summary>
public sealed class NotificationCentreModel : ObservableModel
{
    /// <summary>The accessible name of the live region.</summary>
    public const string LiveRegionName = "Notification status";

    /// <summary>The Show content control label.</summary>
    public const string ShowContentLabel = "Show content";

    /// <summary>The centre heading.</summary>
    public const string Heading = "Notifications";

    /// <summary>The accessible name of the list.</summary>
    public const string ListName = "Notifications list";

    private readonly List<NotificationItemModel> _items = [];
    private bool _showContent;
    private NotificationItemModel? _toast;
    private string _liveRegion = string.Empty;
    private IReadOnlyList<NotificationListEntry> _rows = [];

    /// <summary>Gets the items, newest first, including summaries that the list is not showing.</summary>
    public IReadOnlyList<NotificationItemModel> Items { get; private set; } = [];

    /// <summary>Gets the rows the list binds to. A title-only row has no visible summary until Show content is on.</summary>
    public IReadOnlyList<NotificationListEntry> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>Gets the newest item, shown as the toast.</summary>
    public NotificationItemModel? Toast
    {
        get => _toast;
        private set => SetProperty(ref _toast, value);
    }

    /// <summary>Gets the live-region text. It is the newest title and does not include a hidden summary.</summary>
    public string LiveRegion
    {
        get => _liveRegion;
        private set => SetProperty(ref _liveRegion, value);
    }

    /// <summary>Gets or sets whether summaries are shown. Off is title only.</summary>
    public bool ShowContent
    {
        get => _showContent;
        set
        {
            if (SetProperty(ref _showContent, value))
            {
                Rows = BuildRows();
            }
        }
    }

    /// <summary>Adds an item at the head of the list and announces its title.</summary>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public void Add(NotificationItemModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Insert(0, item);
        Items = [.. _items];
        NotifyPropertyChanged(nameof(Items));
        Toast = item;
        LiveRegion = item.Title;
        Rows = BuildRows();
    }

    private IReadOnlyList<NotificationListEntry> BuildRows()
    {
        var rows = new NotificationListEntry[_items.Count];
        for (var i = 0; i < _items.Count; i++)
        {
            rows[i] = ToRow(_items[i]);
        }

        return rows;
    }

    private NotificationListEntry ToRow(NotificationItemModel item)
    {
        var kind = KindLabel(item.Kind);
        var privacy = item.TitleOnly ? "Title only" : "Show content";
        var visible = ShowContent && !string.IsNullOrEmpty(item.Summary) ? item.Summary : null;
        var name = visible is null
            ? kind + ", " + item.Title + ", session " + item.SessionId
            : kind + ", " + item.Title + ", " + visible + ", session " + item.SessionId;
        return new NotificationListEntry(kind, item.Title, item.SessionId, privacy, visible, item.ApprovalLinkTarget, name);
    }

    private static string KindLabel(NotificationCentreKind kind) => kind switch
    {
        NotificationCentreKind.RunFinished => "Run finished",
        NotificationCentreKind.ApprovalNeeded => "Approval needed",
        NotificationCentreKind.ScheduledTaskFinished => "Scheduled task finished",
        NotificationCentreKind.Failed => "Failed",
        NotificationCentreKind.Missed => "Missed",
        NotificationCentreKind.Waiting => "Waiting",
        _ => "Notification",
    };
}
