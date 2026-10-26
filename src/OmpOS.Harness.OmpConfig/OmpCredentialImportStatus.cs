namespace OmpOS.Harness.OmpConfig;

/// <summary>The outcome of importing an API key into omp's credential store.</summary>
public enum OmpCredentialImportStatus
{
    /// <summary>omp accepted the credential and then resolved it for the provider.</summary>
    Imported = 0,

    /// <summary>The import did not happen, or omp did not confirm it. See the message.</summary>
    Failed = 1,
}
