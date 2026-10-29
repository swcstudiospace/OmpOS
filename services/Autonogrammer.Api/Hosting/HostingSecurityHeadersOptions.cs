namespace Autonogrammer.Api.Hosting;

/// <summary>Binds <c>Hosting:SecurityHeaders</c>.</summary>
internal sealed class HostingSecurityHeadersOptions
{
    /// <summary>Gets or sets whether the security-header middleware runs. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;
}
