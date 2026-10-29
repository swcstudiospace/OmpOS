using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Hosting;

/// <summary>Binds <c>Hosting:Kestrel</c>: connection and request limits applied to the Kestrel server.</summary>
internal sealed class HostingKestrelOptions
{
    /// <summary>Gets or sets the largest accepted request body in bytes. Defaults to 4 MiB; <c>Api:MaxRequestBodyBytes</c> stays the tighter application limit.</summary>
    [Range(1, 1_073_741_824, ErrorMessage = "Hosting:Kestrel:MaxRequestBodyBytes must be between 1 and 1073741824.")]
    public long MaxRequestBodyBytes { get; set; } = 4_194_304;

    /// <summary>Gets or sets how long a client may take to send the request headers, in seconds. Defaults to 30.</summary>
    [Range(1, 300, ErrorMessage = "Hosting:Kestrel:RequestHeadersTimeoutSeconds must be between 1 and 300.")]
    public int RequestHeadersTimeoutSeconds { get; set; } = 30;

    /// <summary>Gets or sets how long an idle keep-alive connection stays open, in seconds. Defaults to 120.</summary>
    [Range(1, 3600, ErrorMessage = "Hosting:Kestrel:KeepAliveTimeoutSeconds must be between 1 and 3600.")]
    public int KeepAliveTimeoutSeconds { get; set; } = 120;

    /// <summary>Gets or sets the connection cap; null means unlimited.</summary>
    [Range(1, 1_000_000, ErrorMessage = "Hosting:Kestrel:MaxConcurrentConnections must be between 1 and 1000000.")]
    public int? MaxConcurrentConnections { get; set; }

    /// <summary>Gets or sets the slowest accepted request-body upload rate in bytes per second; 0 disables the check. Defaults to 240.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Hosting:Kestrel:MinRequestBodyDataRateBytesPerSecond must be between 0 and 1000000.")]
    public int MinRequestBodyDataRateBytesPerSecond { get; set; } = 240;
}
