namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>One problem found while loading a hook file.</summary>
public sealed record WorkspaceHookDiagnosticInfo
{
    /// <summary>Gets <c>warning</c> or <c>error</c>.</summary>
    public required string Severity { get; init; }

    /// <summary>Gets the file the problem is in.</summary>
    public required string File { get; init; }

    /// <summary>Gets a JSON path such as <c>$.hooks.preToolUse[0].command</c>, or <c>$</c>.</summary>
    public required string Location { get; init; }

    /// <summary>Gets what is wrong. It holds no absolute server path.</summary>
    public required string Message { get; init; }
}
