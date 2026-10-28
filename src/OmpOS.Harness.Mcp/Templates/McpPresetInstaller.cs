using System.Buffers;
using System.Text;
using System.Text.Json;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;

namespace OmpOS.Harness.Mcp.Templates;

/// <summary>
/// Adds a <see cref="McpPreset"/> to the OmpOS-owned <c>.ompos/mcp.json</c>, the only MCP file OmpOS writes. It follows the rules of <see cref="McpOwnedConfigWriter"/>: other members and servers of the file are kept, a name that is
/// already taken is refused, an unreadable or invalid file is never overwritten, and the file is rewritten as indented JSON (comments are not kept). The entry holds no credential: a token is a <c>${env:NAME}</c> reference.
/// </summary>
public sealed class McpPresetInstaller
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = McpConfigLimits.MaxJsonDepth,
    };

    private readonly IMcpFileStore _files;

    /// <summary>Creates an installer over a file store.</summary>
    /// <param name="files">The file tree the owned file lives in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public McpPresetInstaller(IMcpFileStore files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    /// <summary>Adds a preset to the file, creating the file when it does not exist.</summary>
    /// <param name="path">The path of the owned file, from <see cref="McpLocations.OwnedProjectFile"/>.</param>
    /// <param name="preset">The preset.</param>
    /// <param name="name">The server name to write; see <see cref="McpServerNames"/>. Pass <see cref="McpPreset.Id"/> unless the user chose another.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="ArgumentException">The preset is not complete (a remote preset without an address, a stdio preset without a command, a token preset without a variable).</exception>
    /// <exception cref="McpCatalogException">The name is not valid or is taken, or the file is not a JSON object, is oversize or cannot be read or written.</exception>
    public async Task AddPresetAsync(string path, McpPreset preset, string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(name);
        CheckComplete(preset);
        if (!McpServerNames.IsValid(name))
        {
            throw new McpCatalogException("The server name must be 1 to 64 letters, digits, '_', '-' or '.'.");
        }

        var read = await _files.ReadAsync(path, McpConfigLimits.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        string text;
        switch (read.Status)
        {
            case McpFileReadStatus.Missing:
                text = Render(null, preset, name);
                break;
            case McpFileReadStatus.Ok:
                text = Render(read.Content, preset, name);
                break;
            case McpFileReadStatus.TooLarge:
                throw new McpCatalogException("The .ompos/mcp.json file is too large to change.");
            default:
                throw new McpCatalogException("The .ompos/mcp.json file could not be read.");
        }

        try
        {
            await _files.WriteTextAsync(path, text, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new McpCatalogException("The .ompos/mcp.json file could not be written.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new McpCatalogException("The .ompos/mcp.json file could not be written.", exception);
        }
    }

    private static void CheckComplete(McpPreset preset)
    {
        if (preset.Kind == McpTemplateKind.Http)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.Address, nameof(preset));
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.Command, nameof(preset));
        }

        if (preset.Auth == McpPresetAuth.BearerToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(preset.TokenVariable, nameof(preset));
        }
    }

    private static string Render(ReadOnlyMemory<byte>? existing, McpPreset preset, string name)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            if (existing is null)
            {
                writer.WriteStartObject();
                writer.WriteStartObject("mcpServers");
                WriteServer(writer, preset, name);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            else
            {
                WriteMerged(writer, existing.Value, preset, name);
            }
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static void WriteMerged(Utf8JsonWriter writer, ReadOnlyMemory<byte> existing, McpPreset preset, string name)
    {
        JsonDocument document;
        try
        {
            var bytes = existing.Span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? existing[3..] : existing;
            document = JsonDocument.Parse(bytes, ReadOptions);
        }
        catch (JsonException ex)
        {
            throw new McpCatalogException("The .ompos/mcp.json file is not valid JSON. Fix it first.", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new McpCatalogException("The .ompos/mcp.json file does not hold a JSON object.");
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
                    throw new McpCatalogException("The 'mcpServers' field of .ompos/mcp.json is not an object.");
                }

                writer.WriteStartObject("mcpServers");
                foreach (var server in property.Value.EnumerateObject())
                {
                    if (string.Equals(server.Name, name, StringComparison.Ordinal))
                    {
                        throw new McpCatalogException("A server named '" + name + "' is already in .ompos/mcp.json.");
                    }

                    server.WriteTo(writer);
                }

                WriteServer(writer, preset, name);
                writer.WriteEndObject();
                wrote = true;
            }

            if (!wrote)
            {
                writer.WriteStartObject("mcpServers");
                WriteServer(writer, preset, name);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }

    private static void WriteServer(Utf8JsonWriter writer, McpPreset preset, string name)
    {
        writer.WriteStartObject(name);
        if (preset.Kind == McpTemplateKind.Http)
        {
            writer.WriteString("url", preset.Address);
            if (preset.Auth == McpPresetAuth.BearerToken)
            {
                writer.WriteStartObject("headers");
                writer.WriteString("Authorization", "Bearer ${env:" + preset.TokenVariable + "}");
                writer.WriteEndObject();
            }
        }
        else
        {
            writer.WriteString("command", preset.Command);
            writer.WriteStartArray("args");
            foreach (var argument in preset.Args)
            {
                writer.WriteStringValue(argument);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
