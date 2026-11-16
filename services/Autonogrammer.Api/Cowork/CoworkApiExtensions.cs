#pragma warning disable MA0048 // The plan owns this file for the module extensions and the shared request helpers.
using System.Collections.Concurrent;
using System.Text.Json;
using Autonogrammer.Api.Cowork.Memory;
using Autonogrammer.Api.Cowork.Projects;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>Registers the Cowork project and memory modules. Callers wire these into the host; this type does not edit the composition root.</summary>
internal static class CoworkApiExtensions
{
    /// <summary>Registers project options, the account store, and the per-user project limiter.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoworkProjects(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddProblemDetails();
        services.AddOptions<ProjectsOptions>()
            .Bind(configuration.GetSection(ProjectsOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ProjectsOptions>, ProjectsOptionsValidator>());
        services.TryAddSingleton<PostgresProjectAccountStore>(static provider =>
            new PostgresProjectAccountStore(provider.GetRequiredService<Npgsql.NpgsqlDataSource>()));
        services.TryAddSingleton<CoworkIdempotencyCache>();
        services.AddRateLimiter(options => options.AddPolicy<string, CoworkProjectsRateLimiterPolicy>(CoworkProjectsRateLimiterPolicy.PolicyName));
        return services;
    }

    /// <summary>Maps project and global-instruction routes. They stay unreachable while <c>Cowork:Projects:Enabled</c> is false.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCoworkProjects(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ProjectsEndpoints.Map(endpoints);
        return endpoints;
    }

    /// <summary>Registers memory options, persistent storage, and the per-user memory limiter.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoworkMemory(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<MemoryOptions>()
            .Bind(configuration.GetSection(MemoryOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MemoryOptions>, MemoryOptionsValidator>());
        services.TryAddSingleton<Autonogrammer.Cowork.Memory.IMemoryStore>(static provider =>
            new Autonogrammer.Cowork.Memory.Postgres.PostgresMemoryStore(
                provider.GetRequiredService<Npgsql.NpgsqlDataSource>(),
                provider.GetService<TimeProvider>(),
                provider.GetService<Autonogrammer.Cowork.Memory.IMemoryNoticeSink>()));
        services.TryAddSingleton<CoworkIdempotencyCache>();
        services.AddRateLimiter(options => options.AddPolicy<string, CoworkMemoryRateLimiterPolicy>(CoworkMemoryRateLimiterPolicy.PolicyName));
        return services;
    }

    /// <summary>Maps memory routes. They stay unreachable while <c>Cowork:Memory:Enabled</c> is false.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapCoworkMemory(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        MemoryEndpoints.Map(endpoints);
        return endpoints;
    }
}

/// <summary>Remembers the first create result for an Idempotency-Key scoped to one account.</summary>
internal sealed class CoworkIdempotencyCache
{
    private readonly ConcurrentDictionary<string, CoworkIdempotentResult> _results = new(StringComparer.Ordinal);

    /// <summary>Returns the first result when this account already used the key.</summary>
    /// <param name="scope">The route family.</param>
    /// <param name="accountId">The token subject.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="result">The stored result.</param>
    /// <returns>True when a result was stored.</returns>
    public bool TryGet(string scope, string accountId, string key, out CoworkIdempotentResult result) =>
        _results.TryGetValue(Compose(scope, accountId, key), out result!);

    /// <summary>Stores the first result. A later store for the same key is ignored.</summary>
    /// <param name="scope">The route family.</param>
    /// <param name="accountId">The token subject.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="statusCode">The status of the first result.</param>
    /// <param name="body">The body of the first result.</param>
    public void Remember(string scope, string accountId, string key, int statusCode, string body) =>
        _results.TryAdd(Compose(scope, accountId, key), new CoworkIdempotentResult(statusCode, body));

    private static string Compose(string scope, string accountId, string key) => scope + "\n" + accountId + "\n" + key;
}

/// <summary>A stored create result.</summary>
/// <param name="StatusCode">The HTTP status.</param>
/// <param name="Body">The JSON body.</param>
internal sealed record CoworkIdempotentResult(int StatusCode, string Body);

/// <summary>Shared body checks for Cowork routes. A body that names an account or a folder is refused.</summary>
internal static class CoworkRequestBodies
{
    /// <summary>The largest accepted JSON body, in bytes. It covers a 16 KiB instruction field plus links.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>Returns whether the object names an account or owner.</summary>
    /// <param name="element">The JSON value.</param>
    /// <returns>True when an owner property is present.</returns>
    public static bool NamesAccount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals("owner", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("ownerId", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("account", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("accountId", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the object names a folder. Folders stay on the device.</summary>
    /// <param name="element">The JSON value.</param>
    /// <returns>True when a folder property is present.</returns>
    public static bool NamesFolder(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals("folder", StringComparison.OrdinalIgnoreCase)
                || property.Name.Equals("folders", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
