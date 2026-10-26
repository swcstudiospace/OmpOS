namespace OmpOS.Workspace;

/// <summary>
/// One persisted approval: the repository, the hash of the approved setup file, and when it was approved.
/// </summary>
internal sealed class SetupConsentGrantDocument
{
    public string Repository { get; set; } = string.Empty;

    public string SetupSha256 { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; }
}
