using System.Text.Json;

namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// An A2UI v0.9.1 surface block (kind "a2ui-surface").
/// </summary>
public sealed record A2uiSurfaceBlock : UiBlock
{
    /// <summary>Gets the A2UI surface identifier.</summary>
    public required string SurfaceId { get; init; }

    /// <summary>Gets the raw A2UI payload.</summary>
    public required JsonElement Payload { get; init; }
}
