namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Runs one <c>gsd-tools.cjs query</c> and returns its standard output. Tests substitute a fake to return fixture JSON and to
/// count spawns without starting any process.
/// </summary>
public interface IGsdProcessRunner
{
    /// <summary>
    /// Runs <c>node &lt;gsdToolsPath&gt; query &lt;queryName&gt; [arguments] --project-dir &lt;projectDirectory&gt;</c> and waits for it.
    /// </summary>
    /// <param name="gsdToolsPath">The absolute path of <c>gsd-tools.cjs</c>.</param>
    /// <param name="queryName">The query name, for example <c>roadmap.analyze</c>.</param>
    /// <param name="queryArguments">Further query arguments, passed one by one without a shell.</param>
    /// <param name="projectDirectory">The workspace root that holds <c>.planning</c>.</param>
    /// <param name="cancellationToken">Cancels the run. The process tree is killed and <see cref="OperationCanceledException"/> is thrown.</param>
    /// <returns>The standard output text.</returns>
    /// <exception cref="GsdToolsException">The process could not start, exited non-zero, timed out, or wrote too much output.</exception>
    Task<string> QueryAsync(
        string gsdToolsPath,
        string queryName,
        IReadOnlyList<string> queryArguments,
        string projectDirectory,
        CancellationToken cancellationToken);
}
