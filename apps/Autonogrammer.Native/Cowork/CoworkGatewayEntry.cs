namespace Autonogrammer.Native.Cowork;

/// <summary>The loopback cowork MCP entry.</summary>
/// <param name="Url">The session endpoint.</param>
/// <param name="Bearer">The bearer. It is placed only in the session header list.</param>
internal sealed record CoworkGatewayEntry(Uri Url, string Bearer);
