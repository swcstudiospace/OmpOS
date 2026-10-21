using System.Diagnostics;

namespace OmpOS.Api.Telemetry;

/// <summary>
/// Shared telemetry names for the API host: one <see cref="ActivitySource"/> for traces
/// and one meter name for the <c>ompos.*</c> instruments.
/// </summary>
internal static class OmpTelemetry
{
    /// <summary>Gets the activity-source and meter name.</summary>
    public const string SourceName = "OmpOS.Api";

    /// <summary>Gets the process-wide activity source used for endpoint and harness spans.</summary>
    public static ActivitySource Activities { get; } = new(SourceName);
}
