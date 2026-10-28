namespace Autonogrammer.Workspace;

/// <summary>
/// The persisted consent state. <see cref="Version"/> is bumped when the shape changes; a file with another version is
/// treated as no consent.
/// </summary>
internal sealed class SetupConsentDocument
{
    public int Version { get; set; }

    public List<SetupConsentGrantDocument> Grants { get; set; } = [];
}
