namespace Autonogrammer.Sandbox.Abstractions;

/// <summary>
/// Mount specification.
/// </summary>
public sealed record MountSpec
{
    /// <summary>Gets the source path (container path or volume name).</summary>
    public required string Source { get; init; }

    /// <summary>Gets the target path inside the sandbox.</summary>
    public required string Target { get; init; }

    /// <summary>Gets whether the mount is read-only.</summary>
    public required bool ReadOnly { get; init; }

    /// <summary>Gets the mount type.</summary>
    public required MountType Type { get; init; }
}
