namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>A problem found while discovering conventions.</summary>
public sealed record ConventionDiagnosticInfo
{
    /// <summary>Gets the kind of convention file the problem belongs to.</summary>
    public required ConventionItemType Item { get; init; }

    /// <summary>Gets the path to show, relative to the workspace folder or <c>~/</c> relative.</summary>
    public required string Origin { get; init; }

    /// <summary>Gets what went wrong.</summary>
    public required ConventionDiagnosticCode Code { get; init; }

    /// <summary>Gets one plain sentence that says why. It holds no absolute server path.</summary>
    public required string Reason { get; init; }
}
