namespace Autonogrammer.Contracts.DataPlane;

/// <summary>
/// One row of <c>GET /status/data-plane</c>. It carries the component, its state and a closed reason, and nothing else: no host, port, version or
/// message, so the body can never quote a connection detail.
/// </summary>
/// <param name="Component">The component.</param>
/// <param name="State">The state.</param>
/// <param name="Reason">The closed reason; <see cref="DataPlaneReason.None"/> when the state is plainly healthy.</param>
public sealed record DataPlaneStatus(DataPlaneComponent Component, DataPlaneState State, DataPlaneReason Reason);
