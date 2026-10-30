using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Binds the <c>Hosting</c> configuration section: reverse-proxy trust, transport security headers, Kestrel and request
/// limits, graceful shutdown, server-sent-event keep-alive and log format. Nested sections are validated by
/// <see cref="HostingOptionsValidator"/>, because data-annotation validation does not recurse.
/// </summary>
internal sealed class HostingOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Hosting";

    /// <summary>Gets or sets the proxy-header trust settings.</summary>
    public HostingForwardedHeadersOptions ForwardedHeaders { get; set; } = new();

    /// <summary>Gets or sets the HSTS settings.</summary>
    public HostingHstsOptions Hsts { get; set; } = new();

    /// <summary>Gets or sets the security-header settings.</summary>
    public HostingSecurityHeadersOptions SecurityHeaders { get; set; } = new();

    /// <summary>Gets or sets the Kestrel limits.</summary>
    public HostingKestrelOptions Kestrel { get; set; } = new();

    /// <summary>Gets or sets the default request timeout in seconds. Streaming routes are exempt. Defaults to 30.</summary>
    [Range(1, 3600, ErrorMessage = "Hosting:RequestTimeoutSeconds must be between 1 and 3600.")]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets how long the host waits for in-flight requests on shutdown, in seconds. Defaults to 30.</summary>
    [Range(1, 600, ErrorMessage = "Hosting:ShutdownTimeoutSeconds must be between 1 and 600.")]
    public int ShutdownTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets the server-sent-event settings.</summary>
    public HostingSseOptions Sse { get; set; } = new();

    /// <summary>Gets or sets whether the console logger writes JSON. Null means true outside Development and false in Development.</summary>
    public bool? JsonLogs { get; set; }
}
