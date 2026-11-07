using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Harness.BrowserUse;

/// <summary>Registers the agent browser. No package is added and configuration is not bound here.</summary>
public static class BrowserUseServiceExtensions
{
    /// <summary>
    /// Adds options with <c>ValidateOnStart</c>, the session host and the pipe launcher.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional options delegate. Null leaves the defaults.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddBrowserUse(this IServiceCollection services, Action<BrowserUseOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<BrowserUseOptions>()
            .Configure(options => configure?.Invoke(options))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrowserUseOptions>, BrowserUseOptionsValidator>();
        services.AddSingleton(static provider => provider.GetRequiredService<IOptions<BrowserUseOptions>>().Value);
        services.AddSingleton<IChromiumLauncher, ChromiumProcessLauncher>();
        services.AddSingleton<BrowserSessionHost>();
        return services;
    }

    /// <summary>Builds a provider and resolves the session host. Used to prove the registration composes.</summary>
    /// <param name="configure">An optional options delegate.</param>
    /// <returns>True when the host resolves.</returns>
    public static async Task<bool> CanResolveSessionHostAsync(Action<BrowserUseOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddBrowserUse(configure);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        try
        {
            return provider.GetRequiredService<BrowserSessionHost>() is not null;
        }
        finally
        {
            await provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}
