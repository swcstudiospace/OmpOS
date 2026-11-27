using Microsoft.AspNetCore.Components;
using Autonogrammer.Web.Services;

namespace Autonogrammer.Web.Demo;

/// <summary>Registers the Demo: the in-browser API handler replaces the network. No terminal transport is registered here (Phase 81): the Demo shows no startable terminal and streams no fake PTY chunks, so the real head's unavailable transport answers.</summary>
internal static class DemoRegistration
{
    /// <summary>Registers every service of the web head over a <see cref="DemoApiHandler"/>. No API origin, token or network is involved.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddAutonogrammerWebDemoServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(sp => new DemoWorld(sp.GetRequiredService<TimeProvider>()));
        return services.AddAutonogrammerWebServices(
            sp => new DemoApiHandler(sp.GetRequiredService<DemoWorld>()),
            sp => new Uri(sp.GetRequiredService<NavigationManager>().BaseUri, UriKind.Absolute),
            static _ => new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo (in this browser)"));
    }
}
