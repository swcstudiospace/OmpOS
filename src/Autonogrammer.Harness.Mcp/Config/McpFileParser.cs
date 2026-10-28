using System.Text.Json;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>Reads the server entries of one configuration file. A file that is not valid never throws: it yields a problem sentence and a diagnostic.</summary>
internal static class McpFileParser
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = McpConfigLimits.MaxJsonDepth,
    };

    /// <summary>Parses a file's bytes.</summary>
    /// <param name="content">The file's bytes.</param>
    /// <param name="location">The file's location.</param>
    /// <param name="interpolation">The values variable references in this file expand to.</param>
    /// <param name="diagnostics">Receives every problem.</param>
    /// <param name="problem">A sentence when the whole file is unusable; otherwise <see langword="null"/>.</param>
    /// <returns>The usable servers in file order.</returns>
    public static List<McpServerSpec> Parse(
        ReadOnlyMemory<byte> content,
        McpLocation location,
        McpInterpolationContext interpolation,
        List<McpDiagnostic> diagnostics,
        out string? problem)
    {
        problem = null;
        var servers = new List<McpServerSpec>();
        var bytes = StripBom(content);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, Options);
        }
        catch (JsonException exception)
        {
            // The exception message can quote the text at the failing position, which may be a secret, so only the position is reported.
            var line = exception.LineNumber is { } l ? l + 1 : 0;
            var column = exception.BytePositionInLine is { } c ? c + 1 : 0;
            problem = line > 0
                ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The file is not valid JSON (line {line}, column {column}).")
                : "The file is not valid JSON.";
            diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Error, location.Origin, null, problem));
            return servers;
        }

        using (document)
        {
            ReadDocument(document.RootElement, location, interpolation, diagnostics, servers, out problem);
        }

        return servers;
    }

    private static void ReadDocument(
        JsonElement root,
        McpLocation location,
        McpInterpolationContext interpolation,
        List<McpDiagnostic> diagnostics,
        List<McpServerSpec> servers,
        out string? problem)
    {
        problem = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            problem = "The file does not hold a JSON object.";
            diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Error, location.Origin, null, problem));
            return;
        }

        var key = location.Format == McpConfigFormat.ServersObject ? "servers" : "mcpServers";
        if (location.Format == McpConfigFormat.ServersObject && root.TryGetProperty("inputs", out _))
        {
            diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Warning, location.Origin, null, "The 'inputs' list is not supported. A server that uses ${input:...} is not offered."));
        }

        if (!root.TryGetProperty(key, out var container) || container.ValueKind == JsonValueKind.Null)
        {
            if (location.Format != McpConfigFormat.ClaudeUser)
            {
                diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Info, location.Origin, null, "The file has no '" + key + "' object, so it declares no servers."));
            }

            return;
        }

        if (container.ValueKind != JsonValueKind.Object)
        {
            problem = "The '" + key + "' field is not an object.";
            diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Error, location.Origin, null, problem));
            return;
        }

        var count = 0;
        foreach (var member in container.EnumerateObject())
        {
            if (count >= McpConfigLimits.MaxServersPerFile)
            {
                diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Warning, location.Origin, null, "The file declares more than " + McpConfigLimits.MaxServersPerFile.ToString(System.Globalization.CultureInfo.InvariantCulture) + " servers; the rest are ignored."));
                break;
            }

            count++;
            var spec = new McpServerReader(location, interpolation, diagnostics, member.Name).Read(member.Value);
            if (spec is not null)
            {
                servers.Add(spec);
            }
        }
    }

    private static ReadOnlyMemory<byte> StripBom(ReadOnlyMemory<byte> content)
    {
        ReadOnlySpan<byte> bom = [0xEF, 0xBB, 0xBF];
        return content.Span.StartsWith(bom) ? content[bom.Length..] : content;
    }
}
