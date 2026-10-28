using Autonogrammer.Harness.AgentLoop.Hooks;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>Describes something a hook did that the thread must show: a denial, a missing consent, added context or a continuation.</summary>
public sealed class HookNoticeRaisedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="notice">The notice.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notice"/> is null.</exception>
    public HookNoticeRaisedEventArgs(HookNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notice = notice;
    }

    /// <summary>Gets the notice.</summary>
    public HookNotice Notice { get; }

    /// <summary>Gets the tool call the notice belongs to, or null when it belongs to the conversation.</summary>
    public string? ToolCallId => Notice.ToolCallId;
}
