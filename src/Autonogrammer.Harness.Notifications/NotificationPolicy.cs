namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Decides which requests reach the operating-system surface. The in-app centre keeps every request. The operating-system
/// surface is suppressed while the window is focused, while Do-not-disturb is on, when an identical request repeats within
/// 30 seconds, and after 10 deliveries in one minute. Title only is the default.
/// </summary>
public sealed class NotificationPolicy : INotificationSink
{
    /// <summary>The operating-system surface was suppressed because the window is focused.</summary>
    public const string SuppressedFocused = "NotificationSuppressedFocused";

    /// <summary>The operating-system surface was suppressed because Do-not-disturb is on.</summary>
    public const string DoNotDisturb = "NotificationDnd";

    /// <summary>How long identical requests collapse to one operating-system delivery.</summary>
    public static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(30);

    /// <summary>The rate-cap window.</summary>
    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);

    /// <summary>The most operating-system deliveries allowed inside <see cref="RateWindow"/>.</summary>
    public const int RateCap = 10;

    private readonly Lock _gate = new();
    private readonly NotificationCoalescer _coalescer;
    private readonly TimeProvider _time;
    private readonly List<NotificationRequest> _inApp = [];
    private readonly Queue<DateTimeOffset> _deliveries = new();
    private int _drops;

    /// <summary>Initializes a new instance of the <see cref="NotificationPolicy"/> class.</summary>
    /// <param name="time">The clock. The coalescer uses the same clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    public NotificationPolicy(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
        _coalescer = new NotificationCoalescer(time);
    }

    /// <summary>Gets the coalescer this policy owns.</summary>
    public NotificationCoalescer Coalescer => _coalescer;

    /// <summary>Gets every request kept for the in-app centre, oldest first.</summary>
    public IReadOnlyList<NotificationRequest> InApp
    {
        get
        {
            lock (_gate)
            {
                return [.. _inApp];
            }
        }
    }

    /// <summary>Gets how many requests the rate cap has dropped from the operating-system surface.</summary>
    public int OperatingSystemDrops
    {
        get
        {
            lock (_gate)
            {
                return _drops;
            }
        }
    }

    /// <inheritdoc />
    public NotificationReceipt Publish(NotificationRequest request, bool windowFocused, bool doNotDisturb, bool showContent = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _inApp.Add(request);
            var summary = RevealSummary(request, showContent);
            if (windowFocused)
            {
                return Receipt(request, false, SuppressedFocused, summary);
            }

            if (doNotDisturb)
            {
                return Receipt(request, false, DoNotDisturb, summary);
            }

            var key = Identity(request);
            if (_coalescer.IsDuplicate(key, CoalesceWindow))
            {
                return Receipt(request, false, null, summary);
            }

            PruneRate();
            if (_deliveries.Count >= RateCap)
            {
                _drops++;
                return Receipt(request, false, null, summary);
            }

            _coalescer.Remember(key);
            _deliveries.Enqueue(_time.GetUtcNow());
            return Receipt(request, true, null, summary);
        }
    }

    private NotificationReceipt Receipt(NotificationRequest request, bool deliver, string? suppression, string? summary) =>
        new(true, deliver, suppression, _drops, request.Title, summary);

    private void PruneRate()
    {
        var now = _time.GetUtcNow();
        while (_deliveries.Count > 0 && now - _deliveries.Peek() >= RateWindow)
        {
            _deliveries.Dequeue();
        }
    }

    private static string? RevealSummary(NotificationRequest request, bool showContent)
    {
        var reveal = showContent || request.Privacy == NotificationPrivacy.ShowContent;
        return reveal && request.Summary.Length > 0 ? request.Summary : null;
    }

    private static string Identity(NotificationRequest request) =>
        string.Join('\u001f', request.Kind.ToString(), request.Title, request.Summary, request.SessionId);
}
