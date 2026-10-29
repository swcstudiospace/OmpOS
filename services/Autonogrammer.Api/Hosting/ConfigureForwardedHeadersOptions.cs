using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Hosting;

/// <summary>
/// Builds <see cref="ForwardedHeadersOptions"/> from <see cref="HostingOptions"/>. The limit is always one hop. The framework's
/// loopback-only default trust lists are cleared and replaced explicitly, so trust is never granted by accident.
/// </summary>
internal sealed class ConfigureForwardedHeadersOptions : IConfigureOptions<ForwardedHeadersOptions>
{
    private readonly IOptions<HostingOptions> _hosting;

    /// <summary>Initializes a new instance of the <see cref="ConfigureForwardedHeadersOptions"/> class.</summary>
    /// <param name="hosting">The hosting options.</param>
    public ConfigureForwardedHeadersOptions(IOptions<HostingOptions> hosting)
    {
        _hosting = hosting;
    }

    /// <inheritdoc />
    public void Configure(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var forwarded = _hosting.Value.ForwardedHeaders;
        options.ForwardLimit = 1;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        if (forwarded.Mode == ForwardedHeadersMode.None)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        if (forwarded.Mode == ForwardedHeadersMode.KnownNetworks)
        {
            foreach (var network in forwarded.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        }
    }
}
