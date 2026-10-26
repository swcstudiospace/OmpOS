namespace OmpOS.Harness.AgentLoop.Launch;

/// <summary>What a terminal launch adds to the plugin arguments of a plan: an optional resume and an optional first prompt.</summary>
public sealed record OmpTerminalOptions
{
    /// <summary>
    /// Gets the native omp session reference to resume (<c>--resume &lt;id&gt;</c>), or null to start a new session. A
    /// session is never continued implicitly: omp's <c>--continue</c> is not used.
    /// </summary>
    public string? NativeSessionId { get; init; }

    /// <summary>
    /// Gets the first prompt, passed as the last positional argument (omp starts a model turn with it), or null or blank for
    /// none. It must not start with <c>-</c> (omp would read it as a flag) or <c>@</c> (omp would read it as a file).
    /// </summary>
    public string? InitialPrompt { get; init; }
}
