namespace OmpOS.Parser;

/// <summary>
/// Caller-supplied session facts that steer recognizer decisions.
/// </summary>
public sealed record PromptContext
{
    /// <summary>Gets a value indicating whether a prompt is currently running on the session.</summary>
    public bool IsSessionRunning { get; init; }

    /// <summary>Gets a value indicating whether the session has completed at least one prompt.</summary>
    public bool HasCompletedPrompt { get; init; }
}
