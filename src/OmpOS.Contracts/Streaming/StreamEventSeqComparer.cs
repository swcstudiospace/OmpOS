namespace OmpOS.Contracts.Streaming;

/// <summary>
/// Orders <see cref="StreamEvent"/> by <see cref="StreamEvent.Seq"/> ascending.
/// Equal Seq values compare as equal and are never merged; de-duplication by
/// Seq is a client concern. Sorting a list is not stable, so code
/// needing stable order must sort by (Seq, insertion index).
/// </summary>
public sealed class StreamEventSeqComparer : IComparer<StreamEvent>
{
    /// <summary>Gets the shared comparer instance.</summary>
    public static StreamEventSeqComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare(StreamEvent? x, StreamEvent? y)
    {
        if (x is null)
        {
            return y is null ? 0 : -1;
        }

        if (y is null)
        {
            return 1;
        }

        return x.Seq.CompareTo(y.Seq);
    }
}
