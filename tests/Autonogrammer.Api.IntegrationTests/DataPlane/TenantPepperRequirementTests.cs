using Autonogrammer.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// The tenant hash keys on the configured pepper when the session store is PostgreSQL. A host with no pepper refuses to start, and a host with a pepper hashes
/// one tenant id to the same value on every start, so a tenant's binding survives a restart.
/// </summary>
public sealed class TenantPepperRequirementTests
{
    private const string TenantId = "tenant-pepper-probe";

    /// <summary>A PostgreSQL connection string that is never dialled: the missing pepper must stop startup before any connection is made.</summary>
    private const string UnreachableConnection = "Host=127.0.0.1;Port=1;Database=unused";

    /// <summary>Verifies a PostgreSQL session store with no pepper fails startup, and the failure names the pepper key.</summary>
    [Fact]
    public void PostgresStoreWithoutPepper_FailsStartupNamingTheKey()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = UnreachableConnection,
        };
        using var factory = new ApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("DataPlane:TenantHashPepper");
    }

    /// <summary>Verifies a PostgreSQL session store with a configured pepper starts on two separate hosts, and both hash one tenant id to the same value.</summary>
    [Fact]
    public void PostgresStoreWithPepper_HashesTheSameTenantOnEveryStart()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = JwtTestHost.PostgresConnectionString(),
            ["DataPlane:TenantHashPepper"] = JwtTestHost.TestTenantHashPepper,
        };

        var first = HashOnFreshHost(config);
        var second = HashOnFreshHost(config);

        first.Length.ShouldBe(32);
        second.ShouldBe(first, "a stable pepper keeps the tenant hash the same across restarts");
    }

    /// <summary>Starts a fresh host with <paramref name="config"/> and returns the tenant hash it computes for <see cref="TenantId"/>.</summary>
    /// <param name="config">The configuration overrides.</param>
    /// <returns>The tenant segment.</returns>
    private static string HashOnFreshHost(Dictionary<string, string?> config)
    {
        using var factory = new ApiFactory(config);
        return factory.Services.GetRequiredService<ITenantHasher>().HashTenant(TenantId);
    }
}
