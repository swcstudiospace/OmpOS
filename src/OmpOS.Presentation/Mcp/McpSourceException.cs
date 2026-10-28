namespace OmpOS.Presentation.Mcp;

/// <summary>An expected failure of an <see cref="IMcpSettingsSource"/>. The message is one plain sentence with no secret; a page shows it as is.</summary>
public sealed class McpSourceException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public McpSourceException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">The reason.</param>
    public McpSourceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and a cause.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public McpSourceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
