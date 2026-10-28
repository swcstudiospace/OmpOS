using System.Diagnostics;

namespace Autonogrammer.Api.Telemetry;

/// <summary>
/// Shared telemetry names for the API host: one <see cref="ActivitySource"/> for traces
/// and one meter name for the <c>autonogrammer.*</c> instruments.
/// </summary>
internal static class ApiTelemetry
{
    /// <summary>Gets the activity-source and meter name.</summary>
    public const string SourceName = "Autonogrammer.Api";

    /// <summary>Gets the process-wide activity source used for endpoint and harness spans.</summary>
    public static ActivitySource Activities { get; } = new(SourceName);
}
