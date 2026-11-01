using Autonogrammer.Coordination.Dragonfly;
using StackExchange.Redis;
using Xunit;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// Probes whether the pinned Dragonfly image enforces per-tenant key-pattern ACLs (<c>~pattern</c> and <c>&amp;channel</c>). The outcome is written to
/// the test output and, when <c>AUTONOGRAMMER_LIVE_DRAGONFLY_ACL_REPORT</c> names a file, to that file. The result never becomes the isolation boundary
/// (D-09): isolation is the dedicated service plus the keyspace helper, and an ACL is defence in depth at most. Run with
/// <c>AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1</c> and <c>AUTONOGRAMMER_TEST_DRAGONFLY_CONNECTION</c>. The temporary ACL user is created and removed inside
/// the test; no ACL user appears in any connection string or startup file.
/// </summary>
[Trait("Category", "Live")]
public sealed class DragonflyAclTests
{
    /// <summary>The environment variable that opts in to this test.</summary>
    public const string GateEnvironmentVariable = "AUTONOGRAMMER_LIVE_DRAGONFLY_ACL";

    /// <summary>The optional environment variable that names a file to receive the outcome line.</summary>
    public const string ReportEnvironmentVariable = "AUTONOGRAMMER_LIVE_DRAGONFLY_ACL_REPORT";

    [Fact]
    public async Task RecordsWhetherTheKeyPatternAclShapeIsEnforcedOnThePinnedImage()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1 and run scripts/test-services.sh up to probe the Dragonfly ACL shape.");
        }

        var tenant = Guid.NewGuid().ToString("N");
        var otherTenant = Guid.NewGuid().ToString("N");
        var userName = "autonogrammer-acl-test-" + Guid.NewGuid().ToString("N");
        var userSecret = Guid.NewGuid().ToString("N");
        var admin = Options();
        using var adminConnection = await ConnectionMultiplexer.ConnectAsync(admin).ConfigureAwait(true);
        var adminDatabase = adminConnection.GetDatabase();
        var ownKey = DragonflyKeyspace.Presence(tenant, "dev-1");
        var foreignKey = DragonflyKeyspace.Presence(otherTenant, "dev-1");

        string outcome;
        try
        {
            outcome = await ProbeAsync(adminDatabase, userName, userSecret, tenant, ownKey, foreignKey).ConfigureAwait(true);
        }
        finally
        {
            _ = await adminDatabase.KeyDeleteAsync([ownKey, foreignKey]).ConfigureAwait(true);
            await DeleteUserAsync(adminDatabase, userName).ConfigureAwait(true);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(outcome);
        var report = Environment.GetEnvironmentVariable(ReportEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(report))
        {
            await File.WriteAllTextAsync(report, outcome + Environment.NewLine, TestContext.Current.CancellationToken).ConfigureAwait(true);
        }
    }

    private static ConfigurationOptions Options()
    {
        var options = ConfigurationOptions.Parse(DragonflyTestEnvironment.RequireConnectionString());
        options.AbortOnConnectFail = true;
        options.ConnectTimeout = 15_000;
        return options;
    }

    private static async Task<string> ProbeAsync(IDatabase admin, string userName, string userSecret, string tenant, RedisKey ownKey, RedisKey foreignKey)
    {
        var pattern = DragonflyKeyspace.Prefix + "*:" + tenant + ":*";
        try
        {
            _ = await admin.ExecuteAsync("ACL", "SETUSER", userName, "on", ">" + userSecret, "~" + pattern, "&" + DragonflyKeyspace.Prefix + "sig:*", "+@all").ConfigureAwait(false);
        }
        catch (RedisServerException exception)
        {
            return "Dragonfly v1.27.1: ACL SETUSER with the key-pattern shape was rejected by the server: " + exception.Message;
        }

        var tenantOptions = Options();
        tenantOptions.User = userName;
        tenantOptions.Password = userSecret;
        ConnectionMultiplexer connection;
        try
        {
            connection = await ConnectionMultiplexer.ConnectAsync(tenantOptions).ConfigureAwait(false);
        }
        catch (RedisConnectionException exception)
        {
            return "Dragonfly v1.27.1: ACL SETUSER was accepted but the client could not connect as that user: " + exception.FailureType + " " + exception.InnerException?.GetType().Name;
        }

        using (connection)
        {
            var database = connection.GetDatabase();
            var ownAllowed = await TrySetAsync(database, ownKey).ConfigureAwait(false);
            var foreignAllowed = await TrySetAsync(database, foreignKey).ConfigureAwait(false);
            return $"Dragonfly v1.27.1: ACL key pattern accepted. Own-tenant key allowed={ownAllowed}; other-tenant key allowed={foreignAllowed}. " +
                $"Pattern isolation enforced={ownAllowed && !foreignAllowed}. The ACL is defence in depth only; it is not the isolation boundary.";
        }
    }

    private static async Task DeleteUserAsync(IDatabase admin, string userName)
    {
        try
        {
            _ = await admin.ExecuteAsync("ACL", "DELUSER", userName).ConfigureAwait(false);
        }
        catch (RedisServerException)
        {
            // The user was never created.
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
