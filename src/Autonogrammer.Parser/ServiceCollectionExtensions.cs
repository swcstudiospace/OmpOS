using Autonogrammer.Parser.Recognizers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Autonogrammer.Parser;

/// <summary>
/// DI registration for the catch-all prompt parser.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in recognizers (orders 10 to 60, plus the fallback) and the <see cref="PromptParser"/> singleton.
    /// Additional <see cref="IPromptRecognizer"/> registrations participate in ordering by their <see cref="IPromptRecognizer.Order"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOmpParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, SlashCommandRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, MentionRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, DirectiveRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, MagicKeywordRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, SteerRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, FollowUpRecognizer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPromptRecognizer, FallbackRecognizer>());
        services.TryAddSingleton<PromptParser>();
        return services;
    }
}
