namespace Autonogrammer.Harness.Mcp.Fixtures;

/// <summary>The MCP protocol revision the fixture servers speak in both stdio and HTTP mode.</summary>
internal static class FixtureProtocol
{
    /// <summary>
    /// The latest revision that still uses the <c>initialize</c> handshake and Streamable HTTP sessions.
    /// It matches the revision the harness client pins, so no version probe or fallback happens.
    /// </summary>
    public const string ProtocolVersion = "2025-11-25";
}
