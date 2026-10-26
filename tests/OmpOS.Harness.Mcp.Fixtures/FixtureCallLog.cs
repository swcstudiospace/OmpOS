namespace OmpOS.Harness.Mcp.Fixtures;

/// <summary>
/// Records each tool call the fixture receives, one tool name per line, in the file named by
/// <see cref="EnvironmentVariable"/>. Tests use it to observe whether a call reached the server.
/// When the variable is unset, nothing is recorded.
/// </summary>
internal static class FixtureCallLog
{
    /// <summary>The environment variable that names the call log file.</summary>
    public const string EnvironmentVariable = "OMPOS_MCP_FIXTURE_CALL_LOG";

    private static readonly Lock Gate = new();

    /// <summary>Appends one received call to the log, if a log file is configured.</summary>
    /// <param name="tool">The tool name that was called.</param>
    public static void Record(string tool)
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // The server handles concurrent calls on several threads; unserialized appends lose lines.
        lock (Gate)
        {
            File.AppendAllText(path, tool + Environment.NewLine);
        }
    }
}
