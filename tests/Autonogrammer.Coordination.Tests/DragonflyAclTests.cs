using Autonogrammer.Coordination.Dragonfly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// Probes whether the pinned Dragonfly image enforces per-tenant key-pattern ACLs (<c>~pattern</c> and <c>&amp;channel</c>). The outcome is written to
/// the test output only. The result never becomes the isolation boundary (D-09): isolation is the dedicated service plus the keyspace helper, and an ACL
/// is defence in depth at most. Run with <c>AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1</c> and <c>AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION</c>. The temporary ACL
/// user is created and removed inside the test; no ACL user appears in any connection string or startup file.
/// </summary>
[Trait("Category", "Live")]
public sealed class DragonflyAclTests
{
    /// <summary>The environment variable that opts in to this test.</summary>
    public const string GateEnvironmentVariable = "AUTONOGRAMMER_LIVE_DRAGONFLY_ACL";

    [Fact]
    public async Task RecordsWhetherTheKeyPatternAclShapeIsEnforcedOnThePinnedImage()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1 and run scripts/test-services.sh up to probe the Dragonfly ACL shape.");
        }

        var output = TestContext.Current.TestOutputHelper;
        var tenant = Guid.NewGuid().ToString("N");
        var otherTenant = Guid.NewGuid().ToString("N");
        var userName = "autonogrammer-acl-test-" + Guid.NewGuid().ToString("N");
        var userSecret = Guid.NewGuid().ToString("N");
        var admin = ConfigurationOptions.Parse(DragonflyTestEnvironment.RequireConnectionString());
        admin.AbortOnConnectFail = true;
        admin.ConnectTimeout = 15_000;
        using var adminConnection = await ConnectionMultiplexer.ConnectAsync(admin).ConfigureAwait(true);
        var adminDatabase = adminConnection.GetDatabase();

        var ownKey = DragonflyKeyspace.Presence(tenant, "dev-1");
        var foreignKey = DragonflyKeyspace.Presence(otherTenant, "dev-1");
        var pattern = DragonflyKeyspace.Prefix + "*:" + tenant + ":*";
        try
        {
            try
            {
                _ = await adminDatabase.ExecuteAsync("ACL", "SETUSER", userName, "on", ">" + userSecret, "~" + pattern, "&" + DragonflyKeyspace.Prefix + "sig:*", "+@all").ConfigureAwait(true);
            }
            catch (RedisServerException exception)
            {
                output?.WriteLine("ACL SETUSER with the key-pattern shape was rejected by the server: " + exception.Message);
                return;
            }

            var tenantConfiguration = ConfigurationOptions.Parse(DragonflyTestEnvironment.RequireConnectionString());
            tenantConfiguration.User = userName;
            tenantConfiguration.Password = userSecret;
            tenantConfiguration.AbortOnConnectFail = true;
            tenantConfiguration.ConnectTimeout = 15_000;
            ConnectionMultiplexer tenantConnection;
            try
            {
                tenantConnection = await ConnectionMultiplexer.ConnectAsync(tenantConfiguration).ConfigureAwait(true);
            }
            catch (RedisConnectionException exception)
            {
                output?.WriteLine("ACL SETUSER was accepted but the client could not connect as that user: " + exception.FailureType + " " + exception.InnerException?.GetType().Name);
                return;
            }

            using var connection = tenantConnection;
            var tenantDatabase = connection.GetDatabase();

            var ownAllowed = await TrySetAsync(tenantDatabase, ownKey).ConfigureAwait(true);
            var foreignAllowed = await TrySetAsync(tenantDatabase, foreignKey).ConfigureAwait(true);
            output?.WriteLine($"ACL key-pattern accepted by SETUSER. Own-tenant key allowed={ownAllowed}; other-tenant key allowed={foreignAllowed}. " +
                $"Enforced pattern isolation = {ownAllowed && !foreignAllowed}. The ACL is defence in depth only; it is not the isolation boundary.");
        }
        finally
        {
            _ = await adminDatabase.KeyDeleteAsync([ownKey, foreignKey]).ConfigureAwait(true);
            try
            {
                _ = await adminDatabase.ExecuteAsync("ACL", "DELUSER", userName).ConfigureAwait(true);
            }
            catch (RedisServerException)
            {
                // The user was never created.
            }
        }
    }

    private static async Task<bool> TrySetAsync(IDatabase database, RedisKey key)
    {
        try
        {
            return await database.StringSetAsync(key, "x", TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        }
        catch (RedisServerException)
        {
            return false;
        }
    }
}
