namespace OmpOS.Harness.Herdr;

/// <summary>
/// The persisted herdr session store: a format version and one entry per session.
/// </summary>
internal sealed class HerdrSessionDocument
{
    /// <summary>The only format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; }

    public List<HerdrStoredSessionDocument> Sessions { get; set; } = [];
}
