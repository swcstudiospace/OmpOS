using System.Collections.Frozen;

namespace OmpOS.Workspace.Conventions;

/// <summary>Scans one base directory (a workspace root or the user's home): the convention directories at its top, and for a workspace root the nested folders that carry their own skills and rules.</summary>
internal sealed class ConventionScanner
{
    private static readonly FrozenSet<string> SkippedFolders = new[]
    {
        "node_modules", "bin", "obj", "target", "dist", "build", "out", "vendor", "venv", "__pycache__",
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly ConventionScanContext _context;
    private readonly ConventionItemScanner _items;
    private readonly ConventionFileReader _reader;

    /// <summary>Initializes a new instance of the <see cref="ConventionScanner"/> class.</summary>
    /// <param name="context">The scan context of the base directory.</param>
    public ConventionScanner(ConventionScanContext context)
    {
        _context = context;
        _items = new ConventionItemScanner(context);
        _reader = new ConventionFileReader(context);
    }

    /// <summary>Scans the base directory, and below a workspace root the nested folders down to the depth bound.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes when the scan has finished.</returns>
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        var top = await _context.Source.ListAsync(_context.BasePath, _context.Options.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
        if (top.Problem is not null)
        {
            _reader.Report(ConventionItemKind.Config, ".", top.ProblemKind, top.Problem);
            return;
        }

        await ScanScopeAsync(string.Empty, top.Entries, cancellationToken).ConfigureAwait(false);
        if (_context.Scope == ConventionScope.Project && _context.Options.MaxNestedDepth > 0)
        {
            await WalkNestedAsync(top.Entries, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsFolder(ConventionEntry entry, string name) => entry.IsDirectory && string.Equals(entry.Name, name, StringComparison.Ordinal);

    private static bool IsFile(ConventionEntry entry, string name) => !entry.IsDirectory && string.Equals(entry.Name, name, StringComparison.Ordinal);

    private async Task WalkNestedAsync(IReadOnlyList<ConventionEntry> rootEntries, CancellationToken cancellationToken)
    {
        var queue = new Queue<(string Relative, int Depth)>();
        Enqueue(queue, string.Empty, 1, rootEntries);
        var listed = 0;
        while (queue.Count > 0 && listed < _context.Options.MaxNestedDirectories)
        {
            var (relative, depth) = queue.Dequeue();
            var list = await _context.Source.ListAsync(ConventionPath.Join(_context.BasePath, relative), _context.Options.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
            listed++;
            await ScanScopeAsync(relative, list.Entries, cancellationToken).ConfigureAwait(false);
            if (depth < _context.Options.MaxNestedDepth)
            {
                Enqueue(queue, relative, depth + 1, list.Entries);
            }
        }
    }

    private static void Enqueue(Queue<(string Relative, int Depth)> queue, string parent, int depth, IReadOnlyList<ConventionEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.IsDirectory && !entry.IsSymbolicLink && entry.Name[0] != '.' && !SkippedFolders.Contains(entry.Name))
            {
                queue.Enqueue((ConventionPath.JoinRelative(parent, entry.Name), depth));
            }
        }
    }

    /// <summary>Scans one scope folder: the root (empty relative path) or a nested folder.</summary>
    private async Task ScanScopeAsync(string scopeDirectory, IReadOnlyList<ConventionEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var name in ConventionDirectories.Names)
        {
            if (entries.Any(entry => IsFolder(entry, name)))
            {
                await ScanConventionDirectoryAsync(scopeDirectory, name, cancellationToken).ConfigureAwait(false);
            }
        }

        if (_context.Scope != ConventionScope.Project)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (!entry.IsDirectory && ConventionItemScanner.IsInstructionFile(entry.Name))
            {
                await _items.ReadInstructionFileAsync(scopeDirectory, string.Empty, entry.Name, cancellationToken).ConfigureAwait(false);
            }
        }

        if (scopeDirectory.Length == 0 && entries.Any(entry => IsFile(entry, ".mcp.json")))
        {
            _items.ListConfigFile(string.Empty, ".mcp.json", isHooks: false);
        }
    }

    private async Task ScanConventionDirectoryAsync(string scopeDirectory, string directory, CancellationToken cancellationToken)
    {
        var relative = ConventionPath.JoinRelative(scopeDirectory, directory);
        var list = await _context.Source.ListAsync(ConventionPath.Join(_context.BasePath, relative), _context.Options.MaxEntriesPerDirectory, cancellationToken).ConfigureAwait(false);
        if (list.Problem is not null)
        {
            _reader.Report(ConventionItemKind.Config, relative, list.ProblemKind, list.Problem);
            return;
        }

        var nested = scopeDirectory.Length > 0;
        foreach (var entry in list.Entries)
        {
            await ScanEntryAsync(scopeDirectory, directory, nested, entry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ScanEntryAsync(string scopeDirectory, string directory, bool nested, ConventionEntry entry, CancellationToken cancellationToken)
    {
        if (IsFolder(entry, "skills"))
        {
            await _items.ScanSkillsAsync(scopeDirectory, directory, cancellationToken).ConfigureAwait(false);
        }
        else if (IsFolder(entry, "rules"))
        {
            await _items.ScanRulesAsync(scopeDirectory, directory, cancellationToken).ConfigureAwait(false);
        }
        else if (!entry.IsDirectory && ConventionItemScanner.IsInstructionFile(entry.Name) && !string.Equals(entry.Name, ".cursorrules", StringComparison.Ordinal))
        {
            await _items.ReadInstructionFileAsync(scopeDirectory, directory, entry.Name, cancellationToken).ConfigureAwait(false);
        }
        else if (!nested)
        {
            await ScanRootOnlyEntryAsync(directory, entry, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Commands, subagents, hooks and MCP files are read at the workspace root and the user's home only, not in nested folders.</summary>
    private async Task ScanRootOnlyEntryAsync(string directory, ConventionEntry entry, CancellationToken cancellationToken)
    {
        if (IsFolder(entry, "commands"))
        {
            await _items.ScanCommandsAsync(directory, cancellationToken).ConfigureAwait(false);
        }
        else if (IsFolder(entry, "agents"))
        {
            await _items.ScanSubagentsAsync(directory, cancellationToken).ConfigureAwait(false);
        }
        else if (IsFile(entry, "hooks.json"))
        {
            _items.ListConfigFile(directory, "hooks.json", isHooks: true);
        }
        else if (IsFile(entry, "mcp.json"))
        {
            _items.ListConfigFile(directory, "mcp.json", isHooks: false);
        }
    }
}
