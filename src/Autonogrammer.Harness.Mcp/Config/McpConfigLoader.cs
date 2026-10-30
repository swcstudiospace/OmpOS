using System.Globalization;

namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>
/// Reads the MCP configuration files users already have (Autonogrammer, Cursor, Claude Code and VS Code style) and merges them. A project file
/// beats a user file, an earlier workspace root beats a later one, and inside a root <c>.autonogrammer</c> beats <c>.cursor</c> beats <c>.mcp.json</c> beats
/// <c>.vscode</c>. The first server with a name wins and the others become an information diagnostic. A missing file is normal, and a
/// malformed, oversize or unreadable file is a diagnostic: loading never throws for a bad file. Nothing is started and nothing is written.
/// </summary>
public sealed class McpConfigLoader
{
    private readonly IMcpFileStore _files;

    /// <summary>Creates a loader over a file store.</summary>
    /// <param name="files">The file tree to read.</param>
    public McpConfigLoader(IMcpFileStore files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
    }

    /// <summary>Reads every location and merges the servers.</summary>
    /// <param name="context">The roots, home and environment.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The effective servers, the files that exist and every problem.</returns>
    public async Task<McpConfigSet> LoadAsync(McpLocatorContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var diagnostics = new List<McpDiagnostic>();
        var files = new List<McpConfigFile>();
        var effective = new Dictionary<string, McpServerSpec>(StringComparer.Ordinal);
        var ordered = new List<McpServerSpec>();

        foreach (var location in McpLocations.Enumerate(context))
        {
            var read = await _files.ReadAsync(location.Path, McpConfigLimits.MaxFileBytes, cancellationToken).ConfigureAwait(false);
            if (read.Status != McpFileReadStatus.Ok)
            {
                if (read.Status != McpFileReadStatus.Missing)
                {
                    Problem(files, diagnostics, location, read.Status == McpFileReadStatus.TooLarge
                        ? "The file is larger than " + (McpConfigLimits.MaxFileBytes / 1024).ToString(CultureInfo.InvariantCulture) + " KiB and was not read."
                        : "The file could not be read. Check its permissions.");
                }

                continue;
            }

            var interpolation = new McpInterpolationContext(
                location.WorkspaceRoot ?? (context.WorkspaceRoots.Count > 0 ? context.WorkspaceRoots[0] : null),
                context.UserHome,
                context.EnvironmentVariables,
                location.Format is McpConfigFormat.ClaudeProject or McpConfigFormat.ClaudeUser);
            var servers = McpFileParser.Parse(read.Content, location, interpolation, diagnostics, out var problem);
            files.Add(new McpConfigFile(location.Origin, location.Scope, location.Format, servers.Count, problem));

            foreach (var server in servers)
            {
                if (effective.TryGetValue(server.Name, out var winner))
                {
                    diagnostics.Add(new McpDiagnostic(
                        McpDiagnosticSeverity.Info,
                        location.Origin,
                        server.Name,
                        "Shadowed by the server of the same name in " + winner.Origin + "."));
                    continue;
                }

                effective[server.Name] = server;
                ordered.Add(server);
            }
        }

        return new McpConfigSet(ordered, files, diagnostics);
    }

    private static void Problem(List<McpConfigFile> files, List<McpDiagnostic> diagnostics, McpLocation location, string problem)
    {
        files.Add(new McpConfigFile(location.Origin, location.Scope, location.Format, 0, problem));
        diagnostics.Add(new McpDiagnostic(McpDiagnosticSeverity.Error, location.Origin, null, problem));
    }
}
