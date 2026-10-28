using System.Globalization;

// Fixture MCP server. "--http <port>" serves Streamable HTTP on 127.0.0.1; otherwise stdio.
if (args is ["--http", var port] && int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber))
{
    await Autonogrammer.Harness.Mcp.Fixtures.FixtureHttpServer.RunAsync(portNumber).ConfigureAwait(false);
}
else
{
    await Autonogrammer.Harness.Mcp.Fixtures.FixtureStdioServer.RunAsync().ConfigureAwait(false);
}
