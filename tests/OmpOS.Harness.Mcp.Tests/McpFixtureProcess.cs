using ModelContextProtocol.Client;

namespace OmpOS.Harness.Mcp.Tests;

/// <summary>Launches the stdio fixture server through the harness connector.</summary>
internal static class McpFixtureProcess
{
    /// <summary>The environment variable the fixture reads to find its call log file.</summary>
    public const string CallLogVariable = "OMPOS_MCP_FIXTURE_CALL_LOG";

    /// <summary>The fixture's launcher, copied next to the test assembly by the project reference.</summary>
    public static string Executable =>
        Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "OmpOS.Harness.Mcp.Fixtures.exe" : "OmpOS.Harness.Mcp.Fixtures");

    /// <summary>Starts a fixture process and completes the MCP handshake.</summary>
    /// <param name="callLogPath">The file the fixture appends each received tool call to.</param>
    /// <param name="cancellationToken">Cancels the connection.</param>
    /// <returns>A connected client.</returns>
    public static Task<McpClient> ConnectAsync(string callLogPath, CancellationToken cancellationToken) =>
        McpClientConnector.ConnectStdioAsync(
            new McpStdioServerOptions(
                Executable,
                Array.Empty<string>(),
                AppContext.BaseDirectory,
                new Dictionary<string, string?>(StringComparer.Ordinal) { [CallLogVariable] = callLogPath }),
            cancellationToken);
}
