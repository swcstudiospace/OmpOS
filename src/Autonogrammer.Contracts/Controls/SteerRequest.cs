namespace Autonogrammer.Contracts.Controls;

/// <summary>The body of <c>POST /sessions/{id}/steer</c>.</summary>
public sealed record SteerRequest
{
    /// <summary>Gets the steering text. Required, not blank, within the configured prompt length.</summary>
    public string? Text { get; init; }

    /// <summary>Gets how the message reaches the run. Required.</summary>
    public SteerMode? Mode { get; init; }
}
