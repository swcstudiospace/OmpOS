using ModelContextProtocol.Server;

namespace Autonogrammer.Harness.Mcp.Fixtures;

/// <summary>Stdio mode: the protocol runs over standard input and output, so console logging is removed.</summary>
internal static class FixtureStdioServer
{
    /// <summary>Runs the stdio server until its input closes.</summary>
    /// <returns>A task that completes when the host stops.</returns>
    public static async Task RunAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.Configure<McpServerOptions>(options => options.ProtocolVersion = FixtureProtocol.ProtocolVersion);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools(FixtureTools.All());

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
