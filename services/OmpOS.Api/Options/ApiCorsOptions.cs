namespace OmpOS.Api.Options;

/// <summary>
/// CORS origins under <c>Api:Cors</c>. An empty list denies all cross-origin traffic.
/// </summary>
internal sealed class ApiCorsOptions
{
    /// <summary>Gets or sets the allowed origins. Empty means no cross-origin requests are allowed.</summary>
    public List<string> Origins { get; set; } = [];
}
