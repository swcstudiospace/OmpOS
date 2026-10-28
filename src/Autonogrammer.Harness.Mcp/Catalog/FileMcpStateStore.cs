using System.Text.Json;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>
/// Keeps <see cref="McpState"/> in one versioned JSON file under the user's configuration directory. Every load reads the file afresh. A file that
/// is damaged, empty or written with another version is treated as empty state (no consent) and moved aside so the next write starts a clean
/// file; loading never throws for a damaged file. A save writes a temporary file in the same directory and replaces the state by rename.
/// </summary>
public sealed class FileMcpStateStore : IMcpStateStore
{
    /// <summary>The only document version this store reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private readonly string _path;

    /// <summary>Creates a store over a file.</summary>
    /// <param name="path">The state file. Its directory is created on the first save.</param>
    public FileMcpStateStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Returns the default state file: <c>Autonogrammer/mcp-state.json</c> under the per-user application data folder.</summary>
    /// <returns>The path.</returns>
    /// <exception cref="InvalidOperationException">The platform has no per-user configuration directory.</exception>
    public static string DefaultPath()
    {
        var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(configuration))
        {
            throw new InvalidOperationException("The platform has no per-user configuration directory for the MCP state file.");
        }

        return Path.Combine(configuration, "Autonogrammer", "mcp-state.json");
    }

    /// <inheritdoc />
    public async Task<McpState> LoadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return McpState.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return McpState.Empty;
        }

        McpStateDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, McpStateJsonContext.Default.McpStateDocument);
        }
        catch (JsonException)
        {
            document = null;
        }

        if (document is null || document.Version != CurrentVersion || document.Enabled is null || document.Consented is null)
        {
            Quarantine();
            return McpState.Empty;
        }

        return McpState.Create(document.Enabled, document.Consented);
    }

    /// <inheritdoc />
    public async Task SaveAsync(McpState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var document = new McpStateDocument
        {
            Version = CurrentVersion,
            Enabled = new Dictionary<string, bool>(state.EnabledOverrides, StringComparer.Ordinal),
            Consented = [.. state.ConsentedIdentities.Order(StringComparer.Ordinal)],
        };
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(stream, document, McpStateJsonContext.Default.McpStateDocument, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private void Quarantine()
    {
        try
        {
            File.Move(_path, _path + ".damaged", overwrite: true);
        }
        catch (IOException)
        {
            // Best effort: if the damaged file cannot be moved aside, the next save replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort, as above.
        }
    }
}
