using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Binds the <c>Api</c> configuration section: CORS origins, prompt and body limits, and rate limits.
/// </summary>
internal sealed class ApiOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Api";

    /// <summary>Gets or sets the CORS settings. Empty origins deny all cross-origin traffic.</summary>
    public ApiCorsOptions Cors { get; set; } = new();

    /// <summary>Gets or sets the maximum accepted prompt length in characters. Defaults to 64000.</summary>
    [Range(1, 10_000_000, ErrorMessage = "Api:MaxPromptLength must be between 1 and 10000000.")]
    public int MaxPromptLength { get; set; } = 64000;

    /// <summary>Gets or sets the maximum request body size in bytes. Defaults to 1 MiB.</summary>
    [Range(1, 1_073_741_824, ErrorMessage = "Api:MaxRequestBodyBytes must be between 1 and 1073741824.")]
    public long MaxRequestBodyBytes { get; set; } = 1048576;

    /// <summary>Gets or sets the rate-limit settings.</summary>
    public ApiRateLimitOptions RateLimit { get; set; } = new();
}
