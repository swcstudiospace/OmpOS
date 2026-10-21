using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Harness</c> configuration section: which harness adapter serves prompts.
/// The safe default (<c>InMemory</c>) ships in <c>appsettings.json</c> so the build-time
/// OpenAPI generator can start the host with no environment variables set.
/// </summary>
internal sealed class HarnessOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Harness";

    /// <summary>Gets or sets the adapter name. Only <c>InMemory</c> exists in Phase 2.</summary>
    [Required(ErrorMessage = "Harness:Adapter is required but was not provided. Set Harness:Adapter (supported value: InMemory).")]
    public string Adapter { get; set; } = string.Empty;
}
