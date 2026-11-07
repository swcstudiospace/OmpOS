namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The diff the panel shows. Every line carries a word and a sign as well as the colour class, so the change reads without
/// colour (D-36). Over the cap the model has counts and no lines; binary kinds have metadata only.
/// </summary>
public sealed class ArtifactDiffModel
{
    /// <summary>Initializes a new instance of the <see cref="ArtifactDiffModel"/> class.</summary>
    /// <param name="lines">The lines, or an empty list when the diff is counts-only or metadata-only.</param>
    /// <param name="addedCount">The number of added lines.</param>
    /// <param name="removedCount">The number of removed lines.</param>
    /// <param name="countsOnly">Whether the cap was exceeded and no lines were built.</param>
    /// <param name="metadataOnly">Whether the kind is binary and only sizes and hashes are compared.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public ArtifactDiffModel(IReadOnlyList<ArtifactDiffLine> lines, int addedCount, int removedCount, bool countsOnly, bool metadataOnly)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegative(addedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(removedCount);
        Lines = lines;
        AddedCount = addedCount;
        RemovedCount = removedCount;
        CountsOnly = countsOnly;
        MetadataOnly = metadataOnly;
    }

    /// <summary>Gets the lines, in order.</summary>
    public IReadOnlyList<ArtifactDiffLine> Lines { get; }

    /// <summary>Gets the number of added lines.</summary>
    public int AddedCount { get; }

    /// <summary>Gets the number of removed lines.</summary>
    public int RemovedCount { get; }

    /// <summary>Gets whether the cap was exceeded and only counts are shown.</summary>
    public bool CountsOnly { get; }

    /// <summary>Gets whether the kind is binary and only metadata is compared.</summary>
    public bool MetadataOnly { get; }

    /// <summary>Gets the one-sentence summary the live region reads.</summary>
    public string Summary
    {
        get
        {
            if (MetadataOnly)
            {
                return "Binary version. Only size and hash differ are compared.";
            }

            return CountsOnly
                ? FormattableString.Invariant($"Diff too large to show lines. Added {AddedCount} lines, removed {RemovedCount}.")
                : FormattableString.Invariant($"Added {AddedCount} lines, removed {RemovedCount}.");
        }
    }

    /// <summary>Returns the sign that marks a change without colour.</summary>
    /// <param name="change">The change.</param>
    /// <returns>"+", "-" or a space.</returns>
    public static string MarkerFor(ArtifactDiffChange change) => change switch
    {
        ArtifactDiffChange.Added => "+",
        ArtifactDiffChange.Removed => "-",
        _ => " ",
    };

    /// <summary>Returns the word that names a change without colour.</summary>
    /// <param name="change">The change.</param>
    /// <returns>"Added", "Removed" or "Unchanged".</returns>
    public static string LabelFor(ArtifactDiffChange change) => change switch
    {
        ArtifactDiffChange.Added => "Added",
        ArtifactDiffChange.Removed => "Removed",
        _ => "Unchanged",
    };
}
