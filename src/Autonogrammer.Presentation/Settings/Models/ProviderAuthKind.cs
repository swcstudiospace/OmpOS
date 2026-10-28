namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>How a provider authenticates.</summary>
public enum ProviderAuthKind
{
    /// <summary>The endpoint needs no credential.</summary>
    None = 0,

    /// <summary>An API key; Autonogrammer can hand one to omp.</summary>
    ApiKey = 1,

    /// <summary>A sign-in that omp performs itself (<c>omp login</c>); Autonogrammer cannot do it for the user.</summary>
    OAuth = 2,

    /// <summary>The source could not tell.</summary>
    Unknown = 3,
}
