using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>
/// Checks whether a server answers: connect, handshake, list tools, disconnect. A check starts a process (stdio) or opens a connection (remote),
/// so it is a live operation: the catalog runs it only for an enabled, approved, complete server and only when the user asks. Tests use a fake.
/// </summary>
public interface IMcpConnectionChecker
{
    /// <summary>Checks one server.</summary>
    /// <param name="server">The server, with its expanded command or URL.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What the check found. An expected failure is a <see cref="McpConnectionOutcome.Failed"/> result, not an exception.</returns>
    Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken);
}
