namespace Autonogrammer.Dispatch.Desktop;

/// <summary>What the connector does after a link ends.</summary>
public enum DispatchLinkAction
{
    /// <summary>Wait a full-jitter delay and connect again.</summary>
    RetryWithJitter,

    /// <summary>Wait the stated delay and connect again.</summary>
    RetryAfter,

    /// <summary>Stop connecting. The state says why.</summary>
    Stop,
}
