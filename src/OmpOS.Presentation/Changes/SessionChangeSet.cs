using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Editor;

namespace OmpOS.Presentation.Changes;

/// <summary>
/// The files a session's tool calls changed, read from the transcript's <see cref="FileEditBlock"/>s: one <see cref="SessionChange"/> per
/// file, with the text before the agent's first edit and the text after its last. The set holds no file and decides nothing; it is a
/// pure function of the blocks and the workspace root, so the same transcript always gives the same set.
/// </summary>
/// <remarks>
/// A file is named by a path relative to the workspace root. The agent may report an absolute path; it is made relative to the root. A path
/// that is outside the root is refused: it is left out of <see cref="Changes"/> and listed in <see cref="RefusedPaths"/>. Edits of a tool
/// call that failed or was cancelled never reached the file and are ignored. A file whose last text equals its first has no net change and is left out.
/// </remarks>
public sealed class SessionChangeSet
{
    private static readonly string[] FailedStatuses = ["failed", "cancelled", "canceled"];

    private SessionChangeSet(IReadOnlyList<SessionChange> changes, IReadOnlyList<string> refusedPaths)
    {
        Changes = changes;
        RefusedPaths = refusedPaths;
    }

    /// <summary>Gets the set of a transcript with no file edit.</summary>
    public static SessionChangeSet Empty { get; } = new([], []);

    /// <summary>Gets the changed files, in the order the transcript first edited them.</summary>
    public IReadOnlyList<SessionChange> Changes { get; }

    /// <summary>Gets the paths the agent reported that are outside the workspace root, as it reported them and without repeats.</summary>
    public IReadOnlyList<string> RefusedPaths { get; }

    /// <summary>Reads the changes out of a transcript's blocks.</summary>
    /// <param name="blocks">The transcript's blocks, in order.</param>
    /// <param name="rootPath">The absolute path of the workspace root.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is null, empty or white space.</exception>
    public static SessionChangeSet Build(IEnumerable<UiBlock> blocks, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var root = Path.GetFullPath(rootPath);
        var materialized = blocks as IReadOnlyList<UiBlock> ?? blocks.ToList();

        var failedCalls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in materialized.OfType<ToolCallBlock>())
        {
            if (FailedStatuses.Contains(call.Status, StringComparer.OrdinalIgnoreCase))
            {
                failedCalls.Add(call.CallId);
            }
            else
            {
                failedCalls.Remove(call.CallId);
            }
        }

        var accumulators = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        var order = new List<Accumulator>();
        var refused = new List<string>();
        foreach (var edit in materialized.OfType<FileEditBlock>())
        {
            if (failedCalls.Contains(edit.CallId))
            {
                continue;
            }

            var path = Relativize(edit.Path, root);
            if (path is null)
            {
                if (!refused.Contains(edit.Path, StringComparer.Ordinal))
                {
                    refused.Add(edit.Path);
                }

                continue;
            }

            if (!accumulators.TryGetValue(path, out var accumulator))
            {
                accumulator = new Accumulator(path, edit.OldText);
                accumulators.Add(path, accumulator);
                order.Add(accumulator);
            }

            accumulator.NewText = edit.NewText;
            accumulator.LastBlockId = edit.Id;
            accumulator.EditCount++;
        }

        var changes = order
            .Where(static item => item.OldText is null || !string.Equals(item.OldText, item.NewText, StringComparison.Ordinal))
            .Select(static item => new SessionChange(item.Path, item.OldText, item.NewText, item.EditCount, item.LastBlockId))
            .ToList();
        return changes.Count == 0 && refused.Count == 0 ? Empty : new SessionChangeSet(changes.AsReadOnly(), refused.AsReadOnly());
    }

    /// <summary>
    /// Makes a reported path relative to the workspace root. An absolute path is taken relative to the root, and a path that leaves the
    /// root, in any spelling, is refused.
    /// </summary>
    /// <param name="reported">The path as the agent reported it.</param>
    /// <param name="root">The absolute, normalised path of the workspace root.</param>
    /// <returns>The normalised relative path, or null when the path is empty, names the root itself, or is outside it.</returns>
    internal static string? Relativize(string reported, string root)
    {
        if (string.IsNullOrWhiteSpace(reported))
        {
            return null;
        }

        var text = reported.Trim();
        if (Path.IsPathRooted(text) || text.StartsWith('/') || text.StartsWith('\\'))
        {
            var relative = Path.GetRelativePath(root, Path.GetFullPath(text));
            if (Path.IsPathRooted(relative)
                || string.Equals(relative, "..", StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.StartsWith("../", StringComparison.Ordinal))
            {
                return null;
            }

            text = relative;
        }

        try
        {
            var normalized = WorkspacePath.Normalize(text);
            return normalized.Length == 0 ? null : normalized;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed class Accumulator(string path, string? oldText)
    {
        public string Path { get; } = path;

        public string? OldText { get; } = oldText;

        public string NewText { get; set; } = string.Empty;

        public string LastBlockId { get; set; } = string.Empty;

        public int EditCount { get; set; }
    }
}
