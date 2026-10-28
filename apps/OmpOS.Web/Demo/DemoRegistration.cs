using Microsoft.AspNetCore.Components;
using OmpOS.Presentation.Terminal;
using OmpOS.Web.Services;

namespace OmpOS.Web.Demo;

/// <summary>Registers the Demo: the in-browser API handler replaces the network, and the terminal transport is the scripted one.</summary>
internal static class DemoRegistration
{
    /// <summary>Registers every service of the web head over a <see cref="DemoApiHandler"/>. No API origin, token or network is involved.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddOmpWebDemoServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(sp => new DemoWorld(sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<IRemoteTerminalTransport>(sp => new DemoTerminalTransport(sp.GetRequiredService<TimeProvider>()));
        return services.AddOmpWebServices(
            sp => new DemoApiHandler(sp.GetRequiredService<DemoWorld>()),
            sp => new Uri(sp.GetRequiredService<NavigationManager>().BaseUri, UriKind.Absolute),
            static _ => new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo (in this browser)"));
    }
}
