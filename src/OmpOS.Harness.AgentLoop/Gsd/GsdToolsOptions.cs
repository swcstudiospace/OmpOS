namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Settings for running the workflow engine's command line tool.
/// </summary>
public sealed class GsdToolsOptions
{
    /// <summary>The default time limit of one query.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The default limit of the bytes kept from a query's standard output.</summary>
    public const int DefaultMaxOutputBytes = 4 * 1024 * 1024;

    /// <summary>Gets the explicit location of <c>gsd-tools.cjs</c> or of the directory that holds <c>bin/gsd-tools.cjs</c>, or null to search the environment and the default install locations.</summary>
    public string? GsdToolsPath { get; init; }

    /// <summary>Gets the node executable. A bare name is resolved from <c>PATH</c> by the operating system.</summary>
    public string NodeExecutable { get; init; } = "node";

    /// <summary>Gets the time limit of one query. The process tree is killed when it passes.</summary>
    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    /// <summary>Gets the most bytes of standard output a query may write. More output kills the process and fails the query.</summary>
    public int MaxOutputBytes { get; init; } = DefaultMaxOutputBytes;
}
