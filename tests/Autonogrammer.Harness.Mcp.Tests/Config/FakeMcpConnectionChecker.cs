using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>A connection check that answers from a script. It starts no process and opens no connection.</summary>
internal sealed class FakeMcpConnectionChecker : IMcpConnectionChecker
{
    private readonly Func<McpServerSpec, McpConnectionResult> _answer;

    /// <summary>Creates a checker that answers every server with <paramref name="answer"/>.</summary>
    /// <param name="answer">The answer for a server.</param>
    public FakeMcpConnectionChecker(Func<McpServerSpec, McpConnectionResult> answer)
    {
        _answer = answer;
    }

    /// <summary>Gets the names of the servers that were checked, in order.</summary>
    public List<string> Checked { get; } = [];

    /// <summary>Gets or sets an exception every check throws, or null.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc />
    public Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken)
    {
        Checked.Add(server.Name);
        return Failure is null ? Task.FromResult(_answer(server)) : Task.FromException<McpConnectionResult>(Failure);
    }
}
