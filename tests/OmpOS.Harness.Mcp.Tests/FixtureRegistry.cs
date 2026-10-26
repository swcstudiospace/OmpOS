namespace OmpOS.Harness.Mcp.Tests;

/// <summary>Builds a registry holding the stdio fixture under the server name <c>fixture</c>.</summary>
internal static class FixtureRegistry
{
    /// <summary>The name the fixture is registered under.</summary>
    public const string ServerName = "fixture";

    /// <summary>Creates a registry with the fixture registered.</summary>
    /// <param name="callLogPath">The fixture's call log file.</param>
    /// <param name="enabledByDefault">The registry default for a session with no explicit setting.</param>
    /// <param name="cancellationToken">Cancels the registration.</param>
    /// <returns>A registry that owns the fixture connection.</returns>
    public static async Task<McpToolRegistry> CreateAsync(string callLogPath, bool enabledByDefault, CancellationToken cancellationToken)
    {
        var registry = new McpToolRegistry(enabledByDefault);
        try
        {
            var client = await McpFixtureProcess.ConnectAsync(callLogPath, cancellationToken).ConfigureAwait(false);
            await registry.RegisterServerAsync(ServerName, client, cancellationToken).ConfigureAwait(false);
            return registry;
        }
        catch
        {
            await registry.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
