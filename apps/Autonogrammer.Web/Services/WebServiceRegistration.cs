using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Terminal;
using Autonogrammer.UI.Components.Auth;
using Autonogrammer.Web.Services.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Autonogrammer.Web.Services;

/// <summary>Registers the web head's API client, event streams, workbench and terminal transport.</summary>
internal static class WebServiceRegistration
{
    /// <summary>
    /// Registers the services of the web head over one shared <see cref="HttpClient"/>. A real build sends the signed-in session of its
    /// <see cref="WebAccountSession"/> (Supabase sign-in); a Demo build sends none. Another <see cref="IApiTokenProvider"/> registered first wins. Settings
    /// pages registered as <see cref="ISettingsPage"/> are added to the Settings window.
    /// A 401 forwards the refused bearer token to the session so a late refusal cannot clear a newer sign-in.
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
        services.AddScoped(sp => new WebAccountSession(sp.GetRequiredService<TimeProvider>(), sp));
        services.AddScoped<IAuthSession>(sp => sp.GetRequiredService<WebAccountSession>());
        services.AddScoped<Autonogrammer.UI.Components.Cowork.ICoworkAccess>(sp => new WebCoworkAccess(sp.GetRequiredService<WebWorkbenchOptions>(), sp.GetService<IAuthSession>(), sp.GetService<IAuthConfigSource>()));
        services.TryAddScoped<IApiTokenProvider>(sp =>
            sp.GetRequiredService<WebWorkbenchOptions>().IsDemo
                ? new NoApiTokenProvider()
                : sp.GetRequiredService<WebAccountSession>());
        services.AddScoped(sp =>
        {
            var tokens = sp.GetRequiredService<IApiTokenProvider>();
            Action<string>? onUnauthorized = tokens is WebAccountSession account ? account.Expire : null;
            return new ApiHttp(new HttpClient(new BearerTokenHandler(tokens, createInnerHandler(sp), onUnauthorized))
            {
                BaseAddress = resolveBaseAddress(sp),
            });
        });
        services.AddScoped<IAuthConfigSource>(sp => new AuthConfigClient(
            sp.GetRequiredService<ApiHttp>().Client,
            sp.GetRequiredService<IConfiguration>()[WebAuthOrigin.ConfigKey],
            sp.GetRequiredService<WebAccountSession>(),
            sp.GetRequiredService<NavigationManager>().BaseUri));
        services.AddScoped<IOmpApiClient>(sp => new OmpApiClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped<IUserEventFeed>(sp => new UserEventFeed(sp.GetRequiredService<ApiHttp>().Client, null, sp.GetRequiredService<TimeProvider>()));
        services.AddScoped(sp => new ApiSessionClient(sp.GetRequiredService<ApiHttp>().Client));
        services.AddScoped(sp => new Artifacts.ArtifactWebClient(sp.GetRequiredService<ApiHttp>(), sp.GetRequiredService<TimeProvider>()));
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
