using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Hosting;

/// <summary>Binds <c>Hosting:Hsts</c>. HSTS is never sent in the Development environment, whatever these values say.</summary>
internal sealed class HostingHstsOptions
{
    /// <summary>Gets or sets whether the <c>Strict-Transport-Security</c> header is sent outside Development. Defaults to true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the header's max-age in days. Defaults to 365.</summary>
    [Range(1, 3650, ErrorMessage = "Hosting:Hsts:MaxAgeDays must be between 1 and 3650.")]
    public int MaxAgeDays { get; set; } = 365;
}
