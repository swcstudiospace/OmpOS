namespace Autonogrammer.Native.Views.Mcp;

/// <summary>
/// The MCP tool source the window owns. The window lists its tools and disposes it when the window closes, so a test can supply
/// a host whose disposal fails and check the window's trace.
/// </summary>
internal interface IMcpRegistryHost : IMcpToolSource, IAsyncDisposable
{
}
