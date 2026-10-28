namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>The brief the server composed. The text is untrusted markdown and is never rendered as markup.</summary>
public sealed record SubstrateBrief
{
    /// <summary>Gets the brief text.</summary>
    public required string Text { get; init; }

    /// <summary>Gets how complete the brief is: <c>complete</c>, <c>degraded</c>, <c>snapshot</c>, or <c>unknown</c> when the server sent no state.</summary>
    public required string State { get; init; }

    /// <summary>Gets the names of the sources the server could not read, empty when none failed.</summary>
    public required IReadOnlyList<string> FailedSources { get; init; }
}
