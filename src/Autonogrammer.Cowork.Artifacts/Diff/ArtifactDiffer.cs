using System.Text;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Autonogrammer.Cowork.Artifacts.Diff;

/// <summary>
/// Diffs two verified versions off the UI thread. Text diffs are capped at 20000 lines and 512 KiB per side.
/// </summary>
public sealed class ArtifactDiffer
{
    /// <summary>The line cap per side.</summary>
    public const int MaxLines = 20_000;

    /// <summary>The byte cap per side.</summary>
    public const int MaxBytes = 512 * 1024;

    private readonly IDiffer _differ = new Differ();

    /// <summary>Diffs two snapshots. A cancelled token throws before any piece list is built.</summary>
    /// <param name="left">The older version.</param>
    /// <param name="right">The newer version.</param>
    /// <param name="cancellationToken">Honoured before the diff starts.</param>
    /// <returns>Counts, metadata, or a DiffPlex piece summary.</returns>
    /// <exception cref="InvalidOperationException">Called on a thread that has a synchronization context.</exception>
    public ArtifactDiff Diff(ArtifactSnapshot left, ArtifactSnapshot right, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        cancellationToken.ThrowIfCancellationRequested();
        if (SynchronizationContext.Current is not null)
        {
            throw new InvalidOperationException("Artifact diff must run off the UI thread.");
        }

        if (IsBinary(left.Kind) || IsBinary(right.Kind))
        {
            return Metadata(left, right);
        }

        var leftText = Encoding.UTF8.GetString(left.Content.Span);
        var rightText = Encoding.UTF8.GetString(right.Content.Span);
        var leftLines = CountLines(leftText);
        var rightLines = CountLines(rightText);
        if (left.Content.Length > MaxBytes || right.Content.Length > MaxBytes || leftLines > MaxLines || rightLines > MaxLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ArtifactDiff
            {
                CountsOnly = true,
                LeftLineCount = leftLines,
                RightLineCount = rightLines,
                LeftByteCount = left.Content.Length,
                RightByteCount = right.Content.Length,
                LeftContentSha256 = left.ContentSha256,
                RightContentSha256 = right.ContentSha256,
            };
        }

        cancellationToken.ThrowIfCancellationRequested();
        var inline = new InlineDiffBuilder(_differ).BuildDiffModel(leftText, rightText);
        var side = new SideBySideDiffBuilder(_differ).BuildDiffModel(leftText, rightText);
        return new ArtifactDiff
        {
            LeftLineCount = leftLines,
            RightLineCount = rightLines,
            LeftByteCount = left.Content.Length,
            RightByteCount = right.Content.Length,
            LeftContentSha256 = left.ContentSha256,
            RightContentSha256 = right.ContentSha256,
            Inserted = inline.Lines.Count(static line => line.Type == ChangeType.Inserted) + side.NewText.Lines.Count(static line => line.Type == ChangeType.Inserted),
            Deleted = inline.Lines.Count(static line => line.Type == ChangeType.Deleted) + side.OldText.Lines.Count(static line => line.Type == ChangeType.Deleted),
        };
    }

    private static ArtifactDiff Metadata(ArtifactSnapshot left, ArtifactSnapshot right) => new()
    {
        MetadataOnly = true,
        LeftByteCount = left.Content.Length,
        RightByteCount = right.Content.Length,
        LeftContentSha256 = left.ContentSha256,
        RightContentSha256 = right.ContentSha256,
    };

    private static bool IsBinary(ArtifactKind kind) => kind == ArtifactKind.Image;

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var lines = 1;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                lines++;
            }
        }

        return text[^1] == '\n' ? lines - 1 : lines;
    }
}
