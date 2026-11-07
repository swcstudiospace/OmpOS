namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Tells <see cref="SecurityHeadersMiddleware"/> to write only <c>X-Content-Type-Options</c>. The render route sets the rest itself.
/// </summary>
internal sealed class OwnResponseSecurityHeadersMetadata
{
    private OwnResponseSecurityHeadersMetadata()
    {
    }

    /// <summary>Gets the shared marker.</summary>
    public static OwnResponseSecurityHeadersMetadata Instance { get; } = new();
}
