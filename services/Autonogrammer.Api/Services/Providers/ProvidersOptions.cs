using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Binds the <c>Providers</c> configuration section. Provider keys are operator-managed on the hosted API (OQ-104): every write switch is
/// off unless the operator turns it on for a single-owner deployment, and no live call is made without it (OQ-71).
/// </summary>
internal sealed class ProvidersOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Providers";

    /// <summary>Gets or sets a value indicating whether <c>PUT /providers/{id}/credential</c> may hand a key to omp. Defaults to false.</summary>
    public bool AllowCredentialWrite { get; set; }

    /// <summary>Gets or sets a value indicating whether the API may change omp's configuration: custom endpoints and the default model. Defaults to false.</summary>
    public bool AllowConfigWrite { get; set; }

    /// <summary>Gets or sets a value indicating whether <c>POST /providers/{id}/test</c> may call a provider address. Defaults to false.</summary>
    public bool AllowLiveTest { get; set; }

    /// <summary>Gets or sets the requests per minute the provider endpoints accept. Defaults to 60.</summary>
    [Range(1, 100_000, ErrorMessage = "Providers:RequestsPerMinute must be between 1 and 100000.")]
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>Gets or sets the seconds a live test waits for an answer. Defaults to 8.</summary>
    [Range(1, 30, ErrorMessage = "Providers:TestTimeoutSeconds must be between 1 and 30.")]
    public int TestTimeoutSeconds { get; set; } = 8;

    /// <summary>Gets or sets the seconds the configured state of the providers is reused. Defaults to 15.</summary>
    [Range(0, 3600, ErrorMessage = "Providers:StateCacheSeconds must be between 0 and 3600.")]
    public int StateCacheSeconds { get; set; } = 15;

    /// <summary>Gets or sets the absolute path of the omp executable, or null to use <c>Harness:Acp:OmpPath</c>, then the search path.</summary>
    public string? OmpPath { get; set; }

    /// <summary>Gets or sets the omp agent directory that holds <c>models.yml</c>, or null for omp's own default. When set it must be an absolute path.</summary>
    public string? AgentDirectory { get; set; }

    /// <summary>Gets a value indicating whether <see cref="OmpPath"/> and <see cref="AgentDirectory"/> hold usable values.</summary>
    /// <returns>True when a set path is absolute.</returns>
    public bool PathsAreAbsolute() =>
        (string.IsNullOrWhiteSpace(OmpPath) || Path.IsPathFullyQualified(OmpPath))
        && (string.IsNullOrWhiteSpace(AgentDirectory) || Path.IsPathFullyQualified(AgentDirectory));
}
