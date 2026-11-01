using Autonogrammer.Contracts.DataPlane;

namespace Autonogrammer.Web.Demo;

/// <summary>The Demo answer of <c>GET /status/data-plane</c>: every component healthy, the ledger off, in the shape of the real contract.</summary>
internal static class DemoDataPlaneStatus
{
    /// <summary>Gets the rows, one per component.</summary>
    internal static DataPlaneStatus[] Rows { get; } =
    [
        new(DataPlaneComponent.Postgres, DataPlaneState.Healthy, DataPlaneReason.None),
        new(DataPlaneComponent.Dragonfly, DataPlaneState.Healthy, DataPlaneReason.None),
        new(DataPlaneComponent.Telemetry, DataPlaneState.Healthy, DataPlaneReason.None),
        new(DataPlaneComponent.Ledger, DataPlaneState.Disabled, DataPlaneReason.NotConfigured),
    ];
}
