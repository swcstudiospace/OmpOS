using Autonogrammer.Harness.Acp.AcpProtocol;

namespace Autonogrammer.Harness.Acp;

/// <summary>The hook notifications an Autonogrammer agent worker sends over the ACP connection (see <see cref="AcpHookNotificationFrames"/>).</summary>
public sealed partial class AcpAgentSession
{
    /// <summary>
    /// Occurs when the agent worker reports a hook run or the state of the session's hooks. It is raised on the session's pump, in wire order,
    /// so a handler must return quickly (queue the work); a handler that throws is ignored. A plain <c>omp acp</c> child never sends the
    /// notification, so for a local session the event never occurs.
    /// </summary>
    public event EventHandler<AcpHookNotificationEventArgs>? HookNotificationReceived;

    private void HandleHookNotification(AcpFrames.Inbound inbound)
    {
        if (!AcpHookNotificationFrames.TryParse(inbound.Params, out var notification) || notification is null)
        {
            return;
        }

        var handlers = HookNotificationReceived;
        if (handlers is null)
        {
            return;
        }

        var args = new AcpHookNotificationEventArgs(notification);
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<AcpHookNotificationEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never break the pump: the notification is a report, not part of the protocol exchange.
                _ = ex;
            }
        }
    }
}
