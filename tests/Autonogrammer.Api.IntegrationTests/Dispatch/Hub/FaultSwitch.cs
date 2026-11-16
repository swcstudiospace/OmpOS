namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>A switch the data-plane suites flip to make Dragonfly unreachable from the hub's seams mid-test.</summary>
internal sealed class FaultSwitch
{
    /// <summary>Gets or sets a value indicating whether the data plane is down.</summary>
    public bool Down { get; set; }
}
