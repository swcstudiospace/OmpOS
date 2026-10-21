namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Snapshot result.
/// </summary>
public sealed record SnapshotResult
{
    /// <summary>Gets the snapshot data (null if not supported).</summary>
    public IReadOnlyList<byte>? Data { get; init; }

    /// <summary>Gets the error if snapshot is not supported.</summary>
    public NotSupportedException? NotSupported { get; init; }

    /// <summary>Creates a successful snapshot result.</summary>
    public static SnapshotResult Success(byte[] data) => new() { Data = data };

    /// <summary>Creates a not-supported snapshot result.</summary>
    public static SnapshotResult NotSupportedResult(string message) =>
        new() { NotSupported = new NotSupportedException(message) };
}
