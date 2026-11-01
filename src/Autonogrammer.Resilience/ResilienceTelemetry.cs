using System.Diagnostics.Metrics;

namespace Autonogrammer.Resilience;

/// <summary>
/// The meter of the data plane (D-63). The library cannot see the API's own telemetry type, so it owns the meter; the API registers
/// <see cref="MeterName"/> once. Every measurement carries only the tags <c>component</c>, <c>operation_class</c>, <c>result</c> and
/// <c>reason</c>, each from a closed set, and never an account, session, device or command id.
/// </summary>
public static class ResilienceTelemetry
{
    /// <summary>The name of the meter, to be passed to <c>AddMeter</c>.</summary>
    public const string MeterName = "Autonogrammer.Resilience";

    private const string ComponentTag = "component";
    private const string OperationClassTag = "operation_class";
    private const string ResultTag = "result";
    private const string ReasonTag = "reason";

    private static readonly Meter DataPlaneMeter = new(MeterName);

    private static readonly Histogram<double> CallDuration = DataPlaneMeter.CreateHistogram<double>(
        "autonogrammer.dataplane.call.duration",
        unit: "ms",
        description: "Wall time of a data-plane call through its resilience pipeline, retries included.");

    private static readonly Counter<long> RetryCount = DataPlaneMeter.CreateCounter<long>(
        "autonogrammer.dataplane.retry.count",
        description: "Retries started by a resilience pipeline.");

    private static readonly Counter<long> BreakerTransitions = DataPlaneMeter.CreateCounter<long>(
        "autonogrammer.dataplane.breaker.transitions",
        description: "State changes of a circuit breaker.");

    private static readonly Counter<long> BulkheadRejected = DataPlaneMeter.CreateCounter<long>(
        "autonogrammer.dataplane.bulkhead.rejected",
        description: "Calls refused because the bulkhead of their operation class was full.");

    /// <summary>Records the duration and outcome of one call.</summary>
    /// <param name="component">The component name.</param>
    /// <param name="operationClass">The class of the call.</param>
    /// <param name="result">The closed result: <c>success</c>, <c>failure</c>, <c>timeout</c>, <c>rejected</c> or <c>cancelled</c>.</param>
    /// <param name="reason">The closed reason.</param>
    /// <param name="durationMs">The duration in milliseconds.</param>
    internal static void RecordCall(string component, OperationClass operationClass, string result, string reason, double durationMs) =>
        CallDuration.Record(
            durationMs,
            new KeyValuePair<string, object?>(ComponentTag, component),
            new KeyValuePair<string, object?>(OperationClassTag, ClassName(operationClass)),
            new KeyValuePair<string, object?>(ResultTag, result),
            new KeyValuePair<string, object?>(ReasonTag, reason));

    /// <summary>Records that a retry started.</summary>
    /// <param name="component">The component name.</param>
    /// <param name="operationClass">The class of the call.</param>
    /// <param name="reason">The closed reason: <c>transient</c>, <c>timeout</c> or <c>serialization</c>.</param>
    internal static void RecordRetry(string component, OperationClass operationClass, string reason) =>
        RetryCount.Add(
            1,
            new KeyValuePair<string, object?>(ComponentTag, component),
            new KeyValuePair<string, object?>(OperationClassTag, ClassName(operationClass)),
            new KeyValuePair<string, object?>(ReasonTag, reason));

    /// <summary>Records a breaker state change.</summary>
    /// <param name="component">The component name.</param>
    /// <param name="result">The state entered: <c>open</c>, <c>half_open</c> or <c>closed</c>.</param>
    /// <param name="reason">The closed reason for the change.</param>
    internal static void RecordBreakerTransition(string component, string result, string reason) =>
        BreakerTransitions.Add(
            1,
            new KeyValuePair<string, object?>(ComponentTag, component),
            new KeyValuePair<string, object?>(ResultTag, result),
            new KeyValuePair<string, object?>(ReasonTag, reason));

    /// <summary>Records a call refused by a full bulkhead.</summary>
    /// <param name="component">The component name.</param>
    /// <param name="operationClass">The class of the call.</param>
    internal static void RecordBulkheadRejected(string component, OperationClass operationClass) =>
        BulkheadRejected.Add(
            1,
            new KeyValuePair<string, object?>(ComponentTag, component),
            new KeyValuePair<string, object?>(OperationClassTag, ClassName(operationClass)),
            new KeyValuePair<string, object?>(ReasonTag, "bulkhead_full"));

    private static string ClassName(OperationClass operationClass) =>
        operationClass == OperationClass.Background ? "background" : "interactive";
}
