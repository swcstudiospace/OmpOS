using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Terminal;
using Autonogrammer.Web.Services.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services;

/// <summary>Registers the web head's API client, event streams, workbench and terminal transport.</summary>
internal static class WebServiceRegistration
{
    /// <summary>
    /// Registers the services of the web head over one shared <see cref="HttpClient"/>. The token provider defaults to none and can be replaced by
    /// registering another <see cref="IApiTokenProvider"/> first (sign-in, OD-03). Settings pages registered as <see cref="ISettingsPage"/> are added to
    /// the Settings window.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="createInnerHandler">Builds the handler that sends the requests: the browser handler for the real API, the Demo handler in Demo mode.</param>
    /// <param name="resolveBaseAddress">Resolves the base address of the API from the service provider.</param>
    /// <param name="options">How the head is composed.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddAutonogrammerWebServices(
        this IServiceCollection services,
        Func<IServiceProvider, HttpMessageHandler> createInnerHandler,
        Func<IServiceProvider, Uri> resolveBaseAddress,
        Func<IServiceProvider, WebWorkbenchOptions> options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(createInnerHandler);
        ArgumentNullException.ThrowIfNull(resolveBaseAddress);
        ArgumentNullException.ThrowIfNull(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IApiTokenProvider, NoApiTokenProvider>();
        services.AddScoped(sp => new ApiHttp(new HttpClient(new BearerTokenHandler(sp.GetRequiredService<IApiTokenProvider>(), createInnerHandler(sp)))
        {
            BaseAddress = resolveBaseAddress(sp),
        }));
        services.AddScoped<IOmpApiClient>(sp => new OmpApiClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped<IUserEventFeed>(sp => new UserEventFeed(sp.GetRequiredService<ApiHttp>().Client, null, sp.GetRequiredService<TimeProvider>()));
        services.AddScoped(sp => new ApiSessionClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped(sp => new SseEventStream(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped(options);
        services.AddWebSettingsPages();
        services.TryAddScoped<IRemoteTerminalTransport, UnavailableTerminalTransport>();
        services.AddScoped(sp => new WebWorkbench(
            sp.GetRequiredService<IOmpApiClient>(),
            sp.GetRequiredService<IUserEventFeed>(),
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<WebWorkbenchOptions>(),
            sp.GetServices<ISettingsPage>()));
        return services;
    }
}
