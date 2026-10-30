namespace Autonogrammer.Workspace.Conventions;

/// <summary>The convention directories that discovery scans at the workspace root and at the user's home, in precedence order.</summary>
public static class ConventionDirectories
{
    /// <summary>The Autonogrammer directory. It wins over every other source.</summary>
    public const string Autonogrammer = ".autonogrammer";

    /// <summary>The omp directory.</summary>
    public const string Omp = ".omp";

    /// <summary>The Cursor directory.</summary>
    public const string Cursor = ".cursor";

    /// <summary>The Claude directory.</summary>
    public const string Claude = ".claude";

    /// <summary>The Codex directory.</summary>
    public const string Codex = ".codex";

    /// <summary>The shared agents directory. It has the lowest precedence.</summary>
    public const string Agents = ".agents";

    /// <summary>Gets the directory names from the highest precedence to the lowest: <c>.autonogrammer</c>, <c>.omp</c>, <c>.cursor</c>, <c>.claude</c>, <c>.codex</c>, <c>.agents</c>.</summary>
    public static IReadOnlyList<string> Names { get; } = [Autonogrammer, Omp, Cursor, Claude, Codex, Agents];
}
