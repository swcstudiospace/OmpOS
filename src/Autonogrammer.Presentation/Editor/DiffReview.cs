using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The review of one file's diff (ADE-11): each hunk gets a decision, and <see cref="Apply"/> computes the file text with the
/// accepted hunks applied to the original. Hunks are applied one at a time against the original, so rejecting a hunk reverts
/// only that hunk.
/// </summary>
/// <remarks>
/// The result is the original with the accepted hunks applied in order. A pending or rejected hunk keeps its original lines, so
/// with every hunk pending the result is the original, and with every hunk accepted it is the diff's new text.
/// </remarks>
public sealed class DiffReview : ObservableModel
{
    private readonly string[] _originalLines;
    private readonly bool _originalEndsWithNewline;
    private int _pendingCount;

    /// <summary>Creates a review of a diff against the original text. Every hunk starts <see cref="HunkDecision.Pending"/>.</summary>
    /// <param name="original">The text the diff was made against.</param>
    /// <param name="diff">The parsed diff.</param>
    /// <exception cref="ArgumentNullException"><paramref name="original"/> or <paramref name="diff"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A hunk does not apply to the original: its lines do not match the original at its range, the hunks overlap or are out of order,
    /// or a hunk disagrees with the original about the line break at the end of the file.
    /// </exception>
    public DiffReview(string original, UnifiedDiff diff)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(diff);

        Original = original;
        _originalLines = SplitText(original, out _originalEndsWithNewline);

        var reviews = new List<HunkReview>(diff.Hunks.Count);
        var cursor = 0;
        for (var number = 1; number <= diff.Hunks.Count; number++)
        {
            var hunk = diff.Hunks[number - 1];
            var index = hunk.OldIndex;
            if (index < cursor || index + hunk.OldCount > _originalLines.Length)
            {
                throw new ArgumentException($"Hunk {number} does not apply to the original text.", nameof(diff));
            }

            var expected = hunk.OldLines;
            for (var offset = 0; offset < expected.Count; offset++)
            {
                if (!string.Equals(_originalLines[index + offset], expected[offset], StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Hunk {number} does not match the original text at line {index + offset + 1}.", nameof(diff));
                }
            }

            if (hunk.OldCount > 0 && index + hunk.OldCount == _originalLines.Length && hunk.OldEndsWithNewline != _originalEndsWithNewline)
            {
                throw new ArgumentException($"Hunk {number} disagrees with the original about the line break at the end of the file.", nameof(diff));
            }

            cursor = index + hunk.OldCount;
            reviews.Add(new HunkReview(number - 1, hunk));
        }

        Hunks = reviews.AsReadOnly();
        _pendingCount = reviews.Count;
    }

    /// <summary>Gets the original text the diff was made against.</summary>
    public string Original { get; }

    /// <summary>Gets the hunks in diff order, each with its decision.</summary>
    public IReadOnlyList<HunkReview> Hunks { get; }

    /// <summary>Gets the number of hunks that are still <see cref="HunkDecision.Pending"/>.</summary>
    public int PendingCount
    {
        get => _pendingCount;
        private set => SetProperty(ref _pendingCount, value);
    }

    /// <summary>Records the decision on one hunk. A later decision replaces an earlier one.</summary>
    /// <param name="index">The zero-based index of the hunk in <see cref="Hunks"/>.</param>
    /// <param name="decision">The decision.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> names no hunk, or <paramref name="decision"/> is not defined.</exception>
    public void Decide(int index, HunkDecision decision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Hunks.Count);
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        Hunks[index].Decision = decision;
        PendingCount = Hunks.Count(review => review.Decision == HunkDecision.Pending);
    }

    /// <summary>Computes the file text: the original with each accepted hunk's new lines in place of its original lines.</summary>
    /// <returns>The resulting text.</returns>
    public string Apply()
    {
        var output = new List<string>();
        var endsWithNewline = _originalEndsWithNewline;
        var cursor = 0;
        foreach (var review in Hunks)
        {
            var hunk = review.Hunk;
            var accepted = review.Decision == HunkDecision.Accept;
            for (; cursor < hunk.OldIndex; cursor++)
            {
                output.Add(_originalLines[cursor]);
            }

            output.AddRange(accepted ? hunk.NewLines : hunk.OldLines);
            cursor = hunk.OldIndex + hunk.OldCount;

            // An accepted hunk that reaches the end of the original decides whether the result ends with a line break.
            if (accepted && cursor == _originalLines.Length)
            {
                endsWithNewline = hunk.NewEndsWithNewline;
            }
        }

        for (; cursor < _originalLines.Length; cursor++)
        {
            output.Add(_originalLines[cursor]);
        }

        if (output.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('\n', output) + (endsWithNewline ? "\n" : string.Empty);
    }

    // The empty text has no lines; text ending in '\n' has no extra empty line after its final break.
    private static string[] SplitText(string text, out bool endsWithNewline)
    {
        if (text.Length == 0)
        {
            endsWithNewline = true;
            return [];
        }

        endsWithNewline = text[^1] == '\n';
        return (endsWithNewline ? text[..^1] : text).Split('\n');
    }
}
