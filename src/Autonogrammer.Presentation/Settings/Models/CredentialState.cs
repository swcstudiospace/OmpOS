namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>Whether omp holds a credential for a provider. The state says nothing about whether the credential still works.</summary>
public enum CredentialState
{
    /// <summary>omp holds a credential for the provider.</summary>
    Present = 0,

    /// <summary>omp holds no credential for the provider.</summary>
    Absent = 1,

    /// <summary>The source could not tell.</summary>
    Unknown = 2,

    /// <summary>The provider needs no credential.</summary>
    NotRequired = 3,
}
