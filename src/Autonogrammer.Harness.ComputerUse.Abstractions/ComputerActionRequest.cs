namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Host-built action. Model text is data inside the tool call that produced the hash, not a field the gate trusts as a target.</summary>
public sealed record ComputerActionRequest
{
    /// <summary>Cowork session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Tool name, such as <c>computer_click</c>.</summary>
    public required string ToolName { get; init; }

    /// <summary>Frame id the model named. The gate checks it is the session's latest frame.</summary>
    public string? FrameId { get; init; }

    /// <summary>Display the scaled coordinates belong to.</summary>
    public string? DisplayId { get; init; }

    /// <summary>Scaled coordinates. <see cref="CoordinateMap"/> is the only scale conversion.</summary>
    public ModelPoint? ScaledCoordinates { get; init; }

    /// <summary>Host-observed target placeholder. Never taken from the model.</summary>
    public ComputerObservedTarget? Target { get; init; }

    /// <summary>Local or Dispatch origin.</summary>
    public required ComputerActionOrigin Origin { get; init; }

    /// <summary>Hash of the host-built action. The ticket is bound to these bytes.</summary>
    public required ReadOnlyMemory<byte> ActionHash { get; init; }
}

