namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>The result of reading one configuration file.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Content">The file's bytes when <paramref name="Status"/> is <see cref="McpFileReadStatus.Ok"/>; empty otherwise.</param>
public sealed record McpFileRead(McpFileReadStatus Status, ReadOnlyMemory<byte> Content)
{
    /// <summary>Gets the result for a file that does not exist.</summary>
    public static McpFileRead Missing { get; } = new(McpFileReadStatus.Missing, ReadOnlyMemory<byte>.Empty);

    /// <summary>Gets the result for a file over the size cap.</summary>
    public static McpFileRead TooLarge { get; } = new(McpFileReadStatus.TooLarge, ReadOnlyMemory<byte>.Empty);

    /// <summary>Gets the result for a file that could not be read.</summary>
    public static McpFileRead Unreadable { get; } = new(McpFileReadStatus.Unreadable, ReadOnlyMemory<byte>.Empty);

    /// <summary>Creates the result for a file that was read.</summary>
    /// <param name="content">The file's bytes.</param>
    /// <returns>An <see cref="McpFileReadStatus.Ok"/> result.</returns>
    public static McpFileRead Read(ReadOnlyMemory<byte> content) => new(McpFileReadStatus.Ok, content);
}
