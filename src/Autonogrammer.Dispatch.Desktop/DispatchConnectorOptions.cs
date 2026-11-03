namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Where the desktop connects and what its approval dialog may show.</summary>
public sealed record DispatchConnectorOptions
{
    /// <summary>Gets the device connect endpoint, <c>wss://…/dispatch/devices/{id}/connect</c>. User information in the URI is refused by the transport.</summary>
    public required Uri Endpoint { get; init; }

    /// <summary>Gets the controller name the local approval dialog shows.</summary>
    public required string ControllerName { get; init; }

    /// <summary>Gets the Computer Use configuration keys.</summary>
    public DispatchComputerUseConfig ComputerUse { get; init; }
}
