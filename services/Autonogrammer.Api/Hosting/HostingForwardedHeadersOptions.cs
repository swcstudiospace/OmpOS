using System.ComponentModel.DataAnnotations;

namespace Autonogrammer.Api.Hosting;

/// <summary>Binds <c>Hosting:ForwardedHeaders</c>: whether and from whom proxy headers are trusted.</summary>
internal sealed class HostingForwardedHeadersOptions
{
    /// <summary>Gets or sets the trust mode. Defaults to <see cref="ForwardedHeadersMode.None"/>.</summary>
    [EnumDataType(typeof(ForwardedHeadersMode), ErrorMessage = "Hosting:ForwardedHeaders:Mode must be None, TrustAllSingleHop or KnownNetworks.")]
    public ForwardedHeadersMode Mode { get; set; } = ForwardedHeadersMode.None;

    /// <summary>Gets or sets the CIDR ranges (for example <c>10.0.0.0/8</c>) of trusted proxies. Required when the mode is <see cref="ForwardedHeadersMode.KnownNetworks"/>.</summary>
    public List<string> KnownNetworks { get; set; } = [];
}
