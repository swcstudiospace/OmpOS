namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>An expected failure of a catalog action: an unknown server, a name that is taken, a file that cannot be changed. The message is one plain sentence with no secret.</summary>
public sealed class McpCatalogException : Exception
{
    /// <summary>Creates an exception with no message.</summary>
    public McpCatalogException()
    {
    }

    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">The reason.</param>
    public McpCatalogException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with a message and a cause.</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public McpCatalogException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
