using System.Globalization;

// Fixture MCP server. "--http <port>" serves Streamable HTTP on 127.0.0.1; otherwise stdio.
if (args is ["--http", var port] && int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber))
{
    await OmpOS.Harness.Mcp.Fixtures.FixtureHttpServer.RunAsync(portNumber).ConfigureAwait(false);
}
else
{
    await OmpOS.Harness.Mcp.Fixtures.FixtureStdioServer.RunAsync().ConfigureAwait(false);
}
