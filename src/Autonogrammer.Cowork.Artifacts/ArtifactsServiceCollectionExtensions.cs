using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>Registers the pure artifact core. A host may register <see cref="IArtifactStore"/> first to override the memory store.</summary>
public static class ArtifactsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <c>Cowork:Artifacts</c>, validates on start, and registers the service. <see cref="TimeProvider.System"/> is added with
    /// <c>TryAddSingleton</c> so a host that already registered it keeps its instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddArtifacts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        _ = services.AddOptions<ArtifactOptions>()
            .Configure<IConfiguration>(static (options, configuration) => Apply(options, configuration.GetSection(ArtifactOptions.SectionName)))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ArtifactOptions>, ArtifactOptionsValidator>());
        services.TryAddSingleton<IArtifactStore, InMemoryArtifactStore>();
        services.TryAddSingleton<ArtifactService>();
        services.TryAddSingleton<Export.ArtifactExporter>();
        services.TryAddSingleton<Diff.ArtifactDiffer>();
        return services;
    }

    private static void Apply(ArtifactOptions options, IConfiguration section)
    {
        Set(section, "MaxTextBytes", value => options.MaxTextBytes = value);
        Set(section, "MaxSvgBytes", value => options.MaxSvgBytes = value);
        Set(section, "MaxMermaidBytes", value => options.MaxMermaidBytes = value);
        Set(section, "MaxImageBytes", value => options.MaxImageBytes = value);
        Set(section, "MaxVersions", value => options.MaxVersions = value);
        Set(section, "MaxArtifacts", value => options.MaxArtifacts = value);
        Set(section, "MaxWritesPerMinutePerSession", value => options.MaxWritesPerMinutePerSession = value);
        if (long.TryParse(section["MaxAccountBytes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var account))
        {
            options.MaxAccountBytes = account;
        }

        var surface = section["Surface"];
        if (!string.IsNullOrWhiteSpace(surface))
        {
            options.Surface = surface;
        }

        if (bool.TryParse(section["Scripts"], out var scripts))
        {
            options.Scripts = scripts;
        }

        if (bool.TryParse(section["AutoOpen"], out var autoOpen))
        {
            options.AutoOpen = autoOpen;
        }
    }

    private static void Set(IConfiguration section, string key, Action<int> assign)
    {
        if (int.TryParse(section[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            assign(value);
        }
    }
}
