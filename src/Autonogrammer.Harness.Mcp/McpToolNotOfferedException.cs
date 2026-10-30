using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Mcp;

/// <summary>The tool is unknown or not enabled for the calling session, so it was never offered to the model. The server is not contacted.</summary>
public sealed class McpToolNotOfferedException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="McpToolNotOfferedException"/> class.</summary>
    public McpToolNotOfferedException()
        : base("An MCP tool is not enabled for this session.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolNotOfferedException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public McpToolNotOfferedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolNotOfferedException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public McpToolNotOfferedException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolNotOfferedException"/> class.</summary>
    /// <param name="sessionId">The session that made the call; carried as the exception context.</param>
    /// <param name="tool">The tool that is not enabled for that session.</param>
    public McpToolNotOfferedException(string sessionId, McpToolId tool)
        : base("MCP tool '" + tool + "' is not enabled for session '" + sessionId + "'.", sessionId, null)
    {
    }
}
