using Autonogrammer.Presentation.Terminal;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Tests;

/// <summary>Registers what the web page needs in a bUnit context: a workbench over the Demo world on a manual clock.</summary>
internal static class DemoPageSetup
{
    /// <summary>Registers the workbench and the terminal transport.</summary>
    /// <param name="services">The bUnit service collection.</param>
    /// <param name="host">The Demo host the workbench reads.</param>
    internal static void Register(IServiceCollection services, DemoTestHost host)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(host);
        services.AddSingleton<TimeProvider>(host.Time);
        services.AddScoped(sp => new WebWorkbench(host.Api, host.Feed, sp.GetRequiredService<IJSRuntime>(), host.Time, new WebWorkbenchOptions(IsDemo: true, ApiHost: "demo"), []));
        services.AddScoped<IRemoteTerminalTransport>(_ => new DemoTerminalTransport(host.Time));
    }
}
