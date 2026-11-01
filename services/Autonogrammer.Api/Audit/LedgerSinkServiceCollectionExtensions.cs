using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Audit;

/// <summary>Registers the audit-ledger sink. The sink is off unless <c>DataPlane:Ledger:Enabled</c> is true.</summary>
internal static class LedgerSinkServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAuditLedger"/> (<see cref="NullAuditLedger"/> unless the ledger is enabled, then <see cref="SubstrateAuditLedger"/>)
    /// and the hosted <see cref="LedgerOutboxPump"/>. The gateway is built only when the ledger is enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLedgerSink(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<NullAuditLedger>();
        services.TryAddSingleton<ILedgerOutboxStore>(static provider =>
        {
            if (!SessionsOptions.IsPostgresStore(provider.GetRequiredService<IOptions<SessionsOptions>>().Value.Store))
            {
                return new MemoryLedgerOutboxStore();
            }

            return new PostgresLedgerOutboxStore(provider.GetRequiredService<NpgsqlDataSource>());
        });
        services.AddSingleton<IAuditLedger>(static provider =>
            provider.GetRequiredService<IOptionsMonitor<DataPlaneOptions>>().CurrentValue.Ledger.Enabled
                ? ActivatorUtilities.CreateInstance<SubstrateAuditLedger>(provider)
                : provider.GetRequiredService<NullAuditLedger>());
        services.AddSingleton<LedgerOutboxPump>();
        services.AddSingleton<IHostedService>(static provider => provider.GetRequiredService<LedgerOutboxPump>());
        services.AddSingleton<ILedgerStatusSource>(static provider => provider.GetRequiredService<LedgerOutboxPump>());
        return services;
    }
}
