using Autonogrammer.Contracts.Hooks;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Harness.Acp;

/// <summary>The data of <see cref="AcpAgentSession.HookNotificationReceived"/>: one notification an Autonogrammer agent worker sent about the hooks of the session.</summary>
public sealed class AcpHookNotificationEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="AcpHookNotificationEventArgs"/> class.</summary>
    /// <param name="notification">The notification.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is null.</exception>
    public AcpHookNotificationEventArgs(AcpHookNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        Notification = notification;
    }

    /// <summary>Gets the notification.</summary>
    public AcpHookNotification Notification { get; }

    /// <summary>
    /// Gets the notification as the payload of a <c>hook</c> stream event, or null when the notification reports a state: a state is not a
    /// run, so it has no place in the run log.
    /// </summary>
    public UnknownPayload? StreamPayload => Notification.Run is { } run ? HookStreamPayload.Create(run) : null;
}
