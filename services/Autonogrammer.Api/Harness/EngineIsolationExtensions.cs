using Autonogrammer.Api.Options;
using Autonogrammer.Worktrees;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Harness;

/// <summary>Registers the validation of the engine isolation keys of <see cref="HarnessOptions"/>.</summary>
internal static class EngineIsolationExtensions
{
    /// <summary>
    /// Registers <see cref="EngineIsolationOptionsValidator"/>, which checks <c>Harness:Acp:Environment</c>,
    /// <c>Harness:Acp:AllowInheritedEnvironment</c> and <c>Harness:Acp:WorkspaceRoot</c> when the <see cref="HarnessOptions"/> are validated on start.
    /// The options themselves are bound by the caller's <c>AddOptions&lt;HarnessOptions&gt;()</c> registration, which must validate on start.
    /// <see cref="AcpHarnessSessionFactory"/> reads the same keys through <see cref="EngineIsolationSettings"/>, so the engine is isolated even
    /// without this registration; the registration is what turns a bad configuration into a start-up failure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment. Isolation defaults depend on whether it is Development.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddEngineIsolation(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        services.AddSingleton<IValidateOptions<HarnessOptions>>(provider =>
            new EngineIsolationOptionsValidator(environment, provider.GetRequiredService<IOptions<WorktreesOptions>>()));
        return services;
    }
}
