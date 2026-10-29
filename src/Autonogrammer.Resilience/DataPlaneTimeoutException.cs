namespace Autonogrammer.Resilience;

/// <summary>
/// A data-plane call that the pipeline ended itself: its deadline expired, its breaker is open or its bulkhead is full. A cancellation the
/// caller asked for is never converted into this exception. It carries the component name and a closed reason, and never a connection string,
/// a host or an inner exception, because a provider message can quote them.
/// </summary>
public sealed class DataPlaneTimeoutException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DataPlaneTimeoutException"/> class.</summary>
    public DataPlaneTimeoutException()
        : this("unknown", DataPlaneTimeoutReason.Timeout)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DataPlaneTimeoutException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public DataPlaneTimeoutException(string message)
        : base(message)
    {
        Component = "unknown";
    }

    /// <summary>Initializes a new instance of the <see cref="DataPlaneTimeoutException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public DataPlaneTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
        Component = "unknown";
    }

    /// <summary>Initializes a new instance of the <see cref="DataPlaneTimeoutException"/> class.</summary>
    /// <param name="component">The data-plane component, for example <c>postgres</c>, <c>dragonfly</c> or <c>ledger</c>.</param>
    /// <param name="reason">Why the pipeline ended the call.</param>
    public DataPlaneTimeoutException(string component, DataPlaneTimeoutReason reason)
        : base(BuildMessage(component, reason))
    {
        Component = component;
        Reason = reason;
    }

    /// <summary>Gets the data-plane component whose call ended. Never a connection string.</summary>
    public string Component { get; }

    /// <summary>Gets why the pipeline ended the call.</summary>
    public DataPlaneTimeoutReason Reason { get; }

    private static string BuildMessage(string component, DataPlaneTimeoutReason reason) => reason switch
    {
        DataPlaneTimeoutReason.CircuitOpen => $"The {component} circuit breaker is open.",
        DataPlaneTimeoutReason.BulkheadFull => $"The {component} bulkhead is full.",
        _ => $"The {component} call exceeded its deadline.",
    };
}
