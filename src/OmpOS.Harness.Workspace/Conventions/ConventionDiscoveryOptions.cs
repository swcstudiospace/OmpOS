namespace OmpOS.Workspace.Conventions;

/// <summary>The bounds of one discovery run. Every bound has a default; a smaller value makes a test small, a larger one never makes discovery unsafe.</summary>
public sealed record ConventionDiscoveryOptions
{
    /// <summary>Gets the shared default options.</summary>
    public static ConventionDiscoveryOptions Default { get; } = new();

    /// <summary>Gets the largest file, in bytes, that discovery reads. A larger file is a <see cref="ConventionDiagnosticKind.TooLarge"/> diagnostic.</summary>
    public int MaxFileBytes { get; init; } = 256 * 1024;

    /// <summary>Gets how deep below a workspace root discovery looks for nested skill and rule scopes. Zero scans the root only.</summary>
    public int MaxNestedDepth { get; init; } = 4;

    /// <summary>Gets the most folders the nested walk of one workspace root lists.</summary>
    public int MaxNestedDirectories { get; init; } = 2000;

    /// <summary>Gets the most entries of one folder that discovery looks at; the rest are ignored.</summary>
    public int MaxEntriesPerDirectory { get; init; } = 2000;
}
