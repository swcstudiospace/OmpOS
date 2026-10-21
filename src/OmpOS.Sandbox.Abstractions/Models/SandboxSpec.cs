namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Sandbox specification.
/// </summary>
public sealed record SandboxSpec
{
    /// <summary>Gets the container image or VM image identifier.</summary>
    public required string Image { get; init; }

    /// <summary>Gets the command to run.</summary>
    public required IReadOnlyList<string> Command { get; init; }

    /// <summary>Gets the working directory.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the environment variables (non-secret).</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>Gets the resource limits.</summary>
    public required ResourceLimits? Limits { get; init; }
}
