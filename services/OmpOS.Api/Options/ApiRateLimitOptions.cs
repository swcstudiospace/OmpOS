using System.ComponentModel.DataAnnotations;

namespace OmpOS.Api.Options;

/// <summary>
/// Rate-limit settings under <c>Api:RateLimit</c>.
/// </summary>
internal sealed class ApiRateLimitOptions
{
    /// <summary>Gets or sets the prompt submissions allowed per minute per session. Defaults to 60.</summary>
    [Range(1, 100_000, ErrorMessage = "Api:RateLimit:PromptsPerMinute must be between 1 and 100000.")]
    public int PromptsPerMinute { get; set; } = 60;

    /// <summary>Gets or sets the session creations allowed per minute. Defaults to 60.</summary>
    [Range(1, 100_000, ErrorMessage = "Api:RateLimit:SessionsPerMinute must be between 1 and 100000.")]
    public int SessionsPerMinute { get; set; } = 60;
}
