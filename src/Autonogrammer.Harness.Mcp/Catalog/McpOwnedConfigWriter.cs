using System.Buffers;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>
/// Adds a server skeleton to the Autonogrammer-owned <c>.autonogrammer/mcp.json</c>. It is the only configuration file Autonogrammer writes: Cursor, Claude and VS Code
/// files are read and never changed. The file is rewritten as indented JSON, so comments in it are not kept. The skeleton is a placeholder the user
/// edits; the catalog switches it off until they do.
/// </summary>
public sealed class McpOwnedConfigWriter
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = McpConfigLimits.MaxJsonDepth,
    };

    private readonly IMcpFileStore _files;

    /// <summary>Creates a writer over a file store.</summary>
    /// <param name="files">The file tree the owned file lives in.</param>
    public McpOwnedConfigWriter(IMcpFileStore files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    /// <summary>Adds a skeleton server to the file, creating the file when it does not exist.</summary>
    /// <param name="path">The path of the owned file, from <see cref="McpLocations.OwnedProjectFile"/>.</param>
    /// <param name="kind">The skeleton.</param>
    /// <param name="name">The server name; see <see cref="McpServerNames"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="McpCatalogException">The name is not valid or is taken, or the file is not a JSON object, is oversize or cannot be read or written.</exception>
    public async Task AddTemplateAsync(string path, McpTemplateKind kind, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);
        if (!McpServerNames.IsValid(name))
        {
            throw new McpCatalogException("The server name must be 1 to 64 letters, digits, '_', '-' or '.'.");
        }

        var read = await _files.ReadAsync(path, McpConfigLimits.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        string text;
        switch (read.Status)
        {
            case McpFileReadStatus.Missing:
                text = Render(null, kind, name);
                break;
            case McpFileReadStatus.Ok:
                text = Render(read.Content, kind, name);
                break;
            case McpFileReadStatus.TooLarge:
                throw new McpCatalogException("The .autonogrammer/mcp.json file is too large to change.");
            default:
                throw new McpCatalogException("The .autonogrammer/mcp.json file could not be read.");
        }

        try
        {
            await _files.WriteTextAsync(path, text, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new McpCatalogException("The .autonogrammer/mcp.json file could not be written.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new McpCatalogException("The .autonogrammer/mcp.json file could not be written.", exception);
        }
    }

    private static string Render(ReadOnlyMemory<byte>? existing, McpTemplateKind kind, string name)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            if (existing is null)
            {
                writer.WriteStartObject();
                writer.WriteStartObject("mcpServers");
                WriteTemplate(writer, kind, name);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            else
            {
                WriteMerged(writer, existing.Value, kind, name);
            }
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static void WriteMerged(Utf8JsonWriter writer, ReadOnlyMemory<byte> existing, McpTemplateKind kind, string name)
    {
        JsonDocument document;
        try
        {
            var bytes = existing.Span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? existing[3..] : existing;
            document = JsonDocument.Parse(bytes, ReadOptions);
        }
        catch (JsonException exception)
        {
            throw new McpCatalogException("The .autonogrammer/mcp.json file is not valid JSON. Fix it first.", exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new McpCatalogException("The .autonogrammer/mcp.json file does not hold a JSON object.");
            }

            writer.WriteStartObject();
            var wrote = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "mcpServers", StringComparison.Ordinal))
                {
                    property.WriteTo(writer);
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new McpCatalogException("The 'mcpServers' field of .autonogrammer/mcp.json is not an object.");
                }

                writer.WriteStartObject("mcpServers");
                foreach (var server in property.Value.EnumerateObject())
                {
                    if (string.Equals(server.Name, name, StringComparison.Ordinal))
                    {
                        throw new McpCatalogException("A server named '" + name + "' is already in .autonogrammer/mcp.json.");
                    }

                    server.WriteTo(writer);
                }

                WriteTemplate(writer, kind, name);
                writer.WriteEndObject();
                wrote = true;
            }

            if (!wrote)
            {
                writer.WriteStartObject("mcpServers");
                WriteTemplate(writer, kind, name);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }

    private static void WriteTemplate(Utf8JsonWriter writer, McpTemplateKind kind, string name)
    {
        writer.WriteStartObject(name);
        if (kind == McpTemplateKind.Stdio)
        {
            writer.WriteString("command", "npx");
            writer.WriteStartArray("args");
            writer.WriteStringValue("-y");
            writer.WriteStringValue("replace-with-mcp-server-package");
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteString("url", "https://mcp.example.com/mcp");
            writer.WriteStartObject("headers");
            writer.WriteString("Authorization", "Bearer ${env:MCP_EXAMPLE_TOKEN}");
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
