using System.Globalization;

namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>The acknowledgement the omp plugin wrote for one directive.</summary>
public sealed record SteeringAck
{
    /// <summary>Gets the identifier of the directive.</summary>
    public required string Id { get; init; }

    /// <summary>Gets what the plugin did.</summary>
    public required SteeringAckStatus Status { get; init; }

    /// <summary>Gets why the directive was not delivered, or null for a delivered directive.</summary>
    public SteeringAckReason? Reason { get; init; }

    /// <summary>Gets the delivery mode the plugin applied, or null when it could not tell.</summary>
    public SteeringDirectiveMode? Mode { get; init; }

    /// <summary>Gets the time the plugin wrote the acknowledgement, or null when it did not say.</summary>
    public DateTimeOffset? At { get; init; }

    /// <summary>Gets whether the session was idle when the message was delivered, or null when the plugin could not tell.</summary>
    public bool? Idle { get; init; }

    /// <summary>Returns a description with the identifier, status and reason.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"SteeringAck {{ Id = {Id}, Status = {Status}, Reason = {Reason} }}");
}
