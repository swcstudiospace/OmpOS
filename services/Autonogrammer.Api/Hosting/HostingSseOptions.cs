using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Hosting;

/// <summary>Binds <c>Hosting:Sse</c>: keep-alive behaviour of the server-sent event streams.</summary>
internal sealed class HostingSseOptions
{
    /// <summary>Gets or sets the idle interval, in seconds, after which a <c>: keepalive</c> comment is written. 0 disables it. Defaults to 15.</summary>
    [Range(0, 3600, ErrorMessage = "Hosting:Sse:HeartbeatSeconds must be between 0 and 3600.")]
    public int HeartbeatSeconds { get; set; } = 15;
}
