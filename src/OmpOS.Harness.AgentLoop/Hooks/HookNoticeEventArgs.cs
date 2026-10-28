namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>Event data of <see cref="IHooksSession.NoticeRaised"/>.</summary>
public sealed class HookNoticeEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="HookNoticeEventArgs"/> class.</summary>
    /// <param name="notice">The notice.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notice"/> is null.</exception>
    public HookNoticeEventArgs(HookNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notice = notice;
    }

    /// <summary>Gets the notice.</summary>
    public HookNotice Notice { get; }
}
