using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Terminal;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services;

/// <summary>Registers the web head's API client, event streams, workbench and terminal transport.</summary>
internal static class WebServiceRegistration
{
    /// <summary>
    /// Registers the services of the web head over one shared <see cref="HttpClient"/>. A real build sends the owner token its <see cref="OwnerTokenSession"/>
    /// holds (the sign-in of an owner-token deployment); a Demo build sends none. Another <see cref="IApiTokenProvider"/> registered first wins. Settings
    /// pages registered as <see cref="ISettingsPage"/> are added to the Settings window.
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
        services.AddScoped(_ => new OwnerTokenSession());
        services.AddScoped<IAuthSession>(sp => sp.GetRequiredService<OwnerTokenSession>());
        services.TryAddScoped<IApiTokenProvider>(sp =>
            sp.GetRequiredService<WebWorkbenchOptions>().IsDemo ? new NoApiTokenProvider() : sp.GetRequiredService<OwnerTokenSession>());
        services.AddScoped(sp =>
        {
            var tokens = sp.GetRequiredService<IApiTokenProvider>();
            Action? onUnauthorized = null;
            if (tokens is OwnerTokenSession session)
            {
                onUnauthorized = session.Expire;
            }

            return new ApiHttp(new HttpClient(new BearerTokenHandler(tokens, createInnerHandler(sp), onUnauthorized))
            {
                BaseAddress = resolveBaseAddress(sp),
            });
        });
        services.AddScoped<IAuthConfigSource>(sp => new AuthConfigClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped<IOmpApiClient>(sp => new OmpApiClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped<IOwnerTokenConnector>(sp => new OwnerTokenConnector(sp.GetRequiredService<OwnerTokenSession>(), sp.GetRequiredService<IOmpApiClient>()));
        services.AddScoped<IUserEventFeed>(sp => new UserEventFeed(sp.GetRequiredService<ApiHttp>().Client, null, sp.GetRequiredService<TimeProvider>()));
        services.AddScoped(sp => new ApiSessionClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped(sp => new SseEventStream(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped(options);
        services.AddWebSettingsPages();
        services.TryAddScoped<IRemoteTerminalTransport, UnavailableTerminalTransport>();
        // One workbench per page instance, not per tab: the page disposes it when it goes away (sign-out, a 401, leaving for another page), which ends its
        // event streams, and the next page instance starts a fresh one.
        services.AddTransient(sp => new WebWorkbench(
            sp.GetRequiredService<IOmpApiClient>(),
            sp.GetRequiredService<IUserEventFeed>(),
            sp.GetRequiredService<IJSRuntime>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<WebWorkbenchOptions>(),
            sp.GetServices<ISettingsPage>()));
        return services;
    }
}
