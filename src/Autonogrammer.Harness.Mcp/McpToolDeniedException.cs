using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Mcp;

/// <summary>The approval decision refused the call. The server was not contacted.</summary>
public sealed class McpToolDeniedException : HarnessException
{
    /// <summary>Initializes a new instance of the <see cref="McpToolDeniedException"/> class.</summary>
    public McpToolDeniedException()
        : base("An MCP tool call was not approved.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolDeniedException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    public McpToolDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolDeniedException"/> class.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public McpToolDeniedException(string message, Exception? innerException)
        : base(message, null, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="McpToolDeniedException"/> class.</summary>
    /// <param name="sessionId">The session that made the call; carried as the exception context.</param>
    /// <param name="tool">The tool whose call was refused.</param>
    public McpToolDeniedException(string sessionId, McpToolId tool)
        : base("MCP tool '" + tool + "' call was not approved.", sessionId, null)
    {
    }
}
