using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.Changes;

namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>
/// Splits a transcript into turns and reads, for each, the files its tool calls changed: the text before the turn's first edit and after its last. It is the per-turn counterpart of
/// <see cref="SessionChangeSet"/> and follows the same rules: a failed or cancelled tool call is ignored, a path is made relative to the workspace root, a path outside the root is dropped,
/// and a file whose last text equals its first has no net change. A turn begins at each <see cref="UserMessageBlock"/>; edits before the first user message form the <see cref="SessionStartKey"/> turn.
/// </summary>
public static class TurnChangeSet
{
    /// <summary>The key of the turn that holds the edits made before any user message.</summary>
    public const string SessionStartKey = "session-start";

    /// <summary>The most characters kept of a turn's label.</summary>
    public const int MaxLabelChars = 200;

    /// <summary>Reads the turns of a transcript that changed files.</summary>
    /// <param name="blocks">The transcript's blocks, in order.</param>
    /// <param name="rootPath">The absolute path of the workspace root.</param>
    /// <returns>The turns that changed at least one file, in transcript order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> is null, empty or white space.</exception>
    public static IReadOnlyList<CheckpointTurnEdits> Build(IEnumerable<UiBlock> blocks, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var root = Path.GetFullPath(rootPath);
        var materialized = blocks as IReadOnlyList<UiBlock> ?? blocks.ToList();
        var failed = FailedToolCalls.Of(materialized);

        var turns = new List<TurnAccumulator>();
        TurnAccumulator? current = null;
        foreach (var block in materialized)
        {
            switch (block)
            {
                case UserMessageBlock user:
                    current = new TurnAccumulator(user.Id, Shorten(user.Text));
                    turns.Add(current);
                    break;
                case FileEditBlock edit when !failed.Contains(edit.CallId):
                    var path = SessionChangeSet.Relativize(edit.Path, root);
                    if (path is null)
                    {
                        break;
                    }

                    if (current is null)
                    {
                        current = new TurnAccumulator(SessionStartKey, string.Empty);
                        turns.Add(current);
                    }

                    current.Add(path, edit);
                    break;
                default:
                    break;
            }
        }

        return [.. turns.Select(static turn => turn.ToEdits()).Where(static turn => turn.Files.Count > 0)];
    }

    private static string Shorten(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= MaxLabelChars ? flat : flat[..MaxLabelChars];
    }

    private sealed class TurnAccumulator(string key, string label)
    {
        private readonly Dictionary<string, FileNetEdit> _files = new(StringComparer.Ordinal);
        private readonly List<FileNetEdit> _order = [];

        public void Add(string path, FileEditBlock edit)
        {
            if (!_files.TryGetValue(path, out var file))
            {
                file = new FileNetEdit(path, edit.OldText);
                _files[path] = file;
                _order.Add(file);
            }

            file.Add(edit);
        }

        public CheckpointTurnEdits ToEdits() =>
            new(
                key,
                label,
                [.. _order.Where(static file => file.HasNetChange).Select(static file => new CheckpointFileEdit(file.Path, file.OldText, file.NewText))]);
    }
}
