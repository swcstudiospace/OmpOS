namespace Autonogrammer.Presentation.Settings;

/// <summary>What one network diagnostics request found out about an endpoint.</summary>
public enum EndpointStatus
{
    /// <summary>The endpoint answered with an HTTP status. Any status counts: the page shows which one.</summary>
    Reachable = 0,

    /// <summary>The name did not resolve or the connection was refused or lost.</summary>
    Unreachable = 1,

    /// <summary>The endpoint did not answer within the time limit.</summary>
    TimedOut = 2,

    /// <summary>The endpoint answered but the secure connection could not be set up.</summary>
    TlsFailed = 3,

    /// <summary>The endpoint was not requested.</summary>
    NotChecked = 4,
}
