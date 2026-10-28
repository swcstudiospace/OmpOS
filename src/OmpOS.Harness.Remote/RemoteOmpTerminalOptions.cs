namespace OmpOS.Harness.Remote;

/// <summary>What the interactive omp of a remote terminal starts with.</summary>
public sealed record RemoteOmpTerminalOptions
{
    /// <summary>Gets the omp session to resume (<c>omp --resume &lt;id&gt;</c>), or <see langword="null"/> for a new session. A non-blank value that does not start with <c>-</c>.</summary>
    public string? NativeSessionId { get; init; }

    /// <summary>Gets the first prompt, or <see langword="null"/> for none. It does not start with <c>-</c> or <c>@</c> and has no line break.</summary>
    public string? InitialPrompt { get; init; }
}
