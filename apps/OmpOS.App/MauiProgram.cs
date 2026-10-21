using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices;
using OmpOS.EngineHost;
using OmpOS.Harness.Acp;

namespace OmpOS.App;

/// <summary>
/// MAUI Blazor Hybrid entry point: a thin shell hosting the shared <c>OmpOS.UI</c> session surface.
/// Desktop targets bind the in-process engine host; mobile targets bind the remote-only host.
/// </summary>
public static partial class MauiProgram
{
    /// <summary>Packaging-time API default for mobile (remote-only) hosts; override per environment at publish.</summary>
    private const string DefaultApiBaseUrl = "https://localhost:5001";

    /// <summary>Builds the cross-platform application.</summary>
    /// <returns>The configured application.</returns>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<OmpOsApp>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddSingleton(static _ => new HttpClient { BaseAddress = new Uri(DefaultApiBaseUrl, UriKind.Absolute) });
        builder.Services.AddSingleton<IEngineHost>(static sp => CreateEngineHost(sp.GetRequiredService<HttpClient>()));
        RegisterPlatformServices(builder.Services);
        return builder.Build();
    }

    private static IEngineHost CreateEngineHost(HttpClient httpClient)
    {
        if (DeviceInfo.Current.Platform == DevicePlatform.Android || DeviceInfo.Current.Platform == DevicePlatform.iOS)
        {
            return new RemoteEngineHost(httpClient);
        }

        return InProcessEngineHost.ForAcp(new OmpAcpClient(), new AcpClientOptions());
    }

    static partial void RegisterPlatformServices(IServiceCollection services);
}
