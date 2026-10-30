using Autonogrammer.Harness.Mcp.Catalog;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>Keeps the MCP state in memory and counts the saves.</summary>
internal sealed class InMemoryMcpStateStore : IMcpStateStore
{
    /// <summary>Gets or sets the stored state.</summary>
    public McpState State { get; set; } = McpState.Empty;

    /// <summary>Gets how many times the state was saved.</summary>
    public int Saves { get; private set; }

    /// <summary>Gets or sets a failure the next save throws.</summary>
    public IOException? SaveFailure { get; set; }

    /// <inheritdoc />
    public Task<McpState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(State);

    /// <inheritdoc />
    public Task SaveAsync(McpState state, CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        State = state;
        Saves++;
        return Task.CompletedTask;
    }
}
