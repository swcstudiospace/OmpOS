namespace Autonogrammer.Sandbox.Abstractions;

/// <summary>
/// Resource limits.
/// </summary>
public sealed record ResourceLimits
{
    /// <summary>Gets the CPU limit in cores.</summary>
    public required double? CpuCores { get; init; }

    /// <summary>Gets the memory limit in bytes.</summary>
    public required long? MemoryBytes { get; init; }

    /// <summary>Gets the pids limit.</summary>
    public required int? PidsLimit { get; init; }
}
