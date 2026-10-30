namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The result of <see cref="OmpCredentialStore.ImportApiKeyAsync"/>. The message never contains the key.</summary>
/// <param name="Status">Whether the key was imported and verified.</param>
/// <param name="Message">A plain sentence for the user.</param>
public sealed record OmpCredentialImportResult(OmpCredentialImportStatus Status, string Message)
{
    /// <summary>Gets a value indicating whether the key was imported and verified.</summary>
    public bool IsImported => Status == OmpCredentialImportStatus.Imported;
}
