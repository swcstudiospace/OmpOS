namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Host-observed permission fact. The advisor turns this into guidance; a missing permission never falls back to another mechanism.</summary>
public sealed record ComputerPermissionStatus
{
    /// <summary>Which permission this status describes.</summary>
    public required ComputerPermissionKind Kind { get; init; }

    /// <summary><see langword="true"/> when the operating system reports the permission is granted.</summary>
    public required bool Granted { get; init; }

    /// <summary>Stable message id for this status. Never a window title or typed text.</summary>
    public required string MessageId { get; init; }
}
