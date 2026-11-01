namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// One contributor's additions to a session launch: process arguments, environment,
/// overlay entries and prompt text, plus an optional process wrapper.
/// </summary>
public sealed record LaunchContribution
{
    /// <summary>Gets the argument vector contributed to the launch.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Gets the environment variables contributed to the launch.</summary>
    public required IReadOnlyDictionary<string, string?> Environment { get; init; }

    /// <summary>Gets the overlay entries contributed to the launch.</summary>
    public required IReadOnlyList<string> OverlayEntries { get; init; }

    /// <summary>Gets the system-prompt paragraph contributed to the launch.</summary>
    public required string SystemPromptParagraph { get; init; }

    /// <summary>Gets the first-prompt block contributed to the launch.</summary>
    public required string FirstPromptBlock { get; init; }

    /// <summary>Gets the optional process wrapper, or <see langword="null"/> for a direct launch.</summary>
    public IProcessLaunchWrapper? ProcessWrapper { get; init; }
}
