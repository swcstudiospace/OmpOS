using Autonogrammer.Harness.Acp;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// The engine child runs with an allow-listed environment outside Development (ADR-0021 D3). The secrets of the data plane are not in the built-in list, so the engine
/// never inherits them (D-66).
/// </summary>
public sealed class DataPlaneEnvironmentIsolationTests
{
    /// <summary>Verifies each data-plane variable is refused by the allowlist policy.</summary>
    /// <param name="name">The environment variable name.</param>
    [Theory]
    [InlineData("Sessions__Postgres__ConnectionString")]
    [InlineData("Sessions__Postgres__MigratorConnectionString")]
    [InlineData("Coordination__Dragonfly__ConnectionString")]
    [InlineData("DataPlane__TenantHashPepper")]
    [InlineData("DataPlane__Ledger__Token")]
    [InlineData("OTEL_EXPORTER_OTLP_HEADERS")]
    [InlineData("DFLY_requirepass")]
    [InlineData("PGPASSWORD")]
    public void Allowlist_RefusesDataPlaneSecrets(string name)
    {
        new EngineEnvironmentPolicy(EngineEnvironmentMode.Allowlist).IsAllowed(name).ShouldBeFalse();
    }
}
