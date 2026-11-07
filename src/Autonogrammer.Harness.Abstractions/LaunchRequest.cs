namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Describes a session launch that contributors enrich with arguments, environment and prompt text.
/// </summary>
public sealed record LaunchRequest
{
    /// <summary>Gets the surface requesting the launch (for example, desktop or automation).</summary>
    public required string Surface { get; init; }

    /// <summary>Gets the session identifier being launched.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the working directory the session starts in.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Gets the folders the session may read without further approval.</summary>
    public required IReadOnlyList<string> TrustedFolders { get; init; }
}
