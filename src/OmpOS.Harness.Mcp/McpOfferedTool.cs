using ModelContextProtocol.Client;

namespace OmpOS.Harness.Mcp;

/// <summary>A tool enabled for one session, paired with the client that serves it.</summary>
/// <param name="Descriptor">The tool as registered.</param>
/// <param name="Client">The connected client for the tool's server.</param>
internal sealed record McpOfferedTool(McpToolDescriptor Descriptor, McpClient Client);
