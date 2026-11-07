using Autonogrammer.Api.DataPlane;
using Autonogrammer.Sessions.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>The <c>migrate</c> verb: usage errors, the key-only failure and the round trip against a throwaway database.</summary>
public sealed class MigrateCommandTests
{
    private const string MigratorKey = "Sessions:Postgres:MigratorConnectionString";

    /// <summary>Verifies the verb is recognised only as the first argument.</summary>
    [Fact]
    public void IsMigrate_NeedsTheVerbFirst()
    {
        MigrateCommand.IsMigrate(["migrate"]).ShouldBeTrue();
        MigrateCommand.IsMigrate(["migrate", "--down", "3"]).ShouldBeTrue();
        MigrateCommand.IsMigrate([]).ShouldBeFalse();
        MigrateCommand.IsMigrate(["--urls", "migrate"]).ShouldBeFalse();
        MigrateCommand.IsMigrate(["Migrate"]).ShouldBeFalse();
    }

    /// <summary>Verifies bad arguments end with exit code 2 and no database is touched.</summary>
    /// <param name="argument">The arguments after the verb.</param>
    [Theory]
    [InlineData("--down")]
    [InlineData("--down|abc")]
    [InlineData("--down|-1")]
    [InlineData("extra")]
    public async Task BadArguments_ExitWithUsage(string argument)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = ["migrate", .. argument.Split('|')];

        var code = await MigrateCommand.RunAsync(args, output, error, TestContext.Current.CancellationToken).ConfigureAwait(true);

        code.ShouldBe(MigrateCommand.Usage);
        error.ToString().ShouldContain("Usage:");
    }

    /// <summary>Verifies a missing connection string ends with exit code 1 and the message names the key only.</summary>
    [Fact]
    public async Task MissingConnectionString_ExitsWithFailure_NamingTheKey()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new ConfigurationBuilder().Build();

        var code = await MigrateCommand.RunAsync(configuration, null, output, error, TestContext.Current.CancellationToken).ConfigureAwait(true);

        code.ShouldBe(MigrateCommand.Failed);
        error.ToString().ShouldContain(MigratorKey);
    }

    /// <summary>Verifies an unreachable database ends with exit code 1 and the output never carries the connection string.</summary>
    [Fact]
    public async Task UnreachableDatabase_ExitsWithFailure_WithoutTheConnectionString()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [MigratorKey] = "Host=127.0.0.1;Port=1;Database=nowhere;Username=u;Password=secret-migrator-password-42;Timeout=1",
                ["DataPlane:Postgres:Resilience:MaxRetries"] = "0",
            })
            .Build();

        var code = await MigrateCommand.RunAsync(configuration, null, output, error, TestContext.Current.CancellationToken).ConfigureAwait(true);

        code.ShouldBe(MigrateCommand.Failed);
        (output.ToString() + error).ShouldNotContain("secret-migrator-password-42");
        (output.ToString() + error).ShouldNotContain("127.0.0.1");
    }

    /// <summary>Verifies migrate, then --down 3, then migrate ends at the compiled head.</summary>
    [Fact]
    public async Task MigrateDownMigrate_RoundTrip_EndsAtTheCompiledHead()
    {
        var testToken = TestContext.Current.CancellationToken;
        var database = await ThrowawayDatabase.CreateAsync(testToken).ConfigureAwait(true);
        await using (database.ConfigureAwait(true))
        {
            string[] up = ["migrate", $"--{MigratorKey}={database.ConnectionString}"];
            string[] down = ["migrate", "--down", "3", $"--{MigratorKey}={database.ConnectionString}"];

            (await RunAsync(up, testToken).ConfigureAwait(true)).Code.ShouldBe(MigrateCommand.Succeeded);
            (await VersionAsync(database, testToken).ConfigureAwait(true)).ShouldBe(MigrationRegistry.Head);

            var reverted = await RunAsync(down, testToken).ConfigureAwait(true);
            reverted.Code.ShouldBe(MigrateCommand.Succeeded);
            (await VersionAsync(database, testToken).ConfigureAwait(true)).ShouldBe(3);

            var again = await RunAsync(up, testToken).ConfigureAwait(true);
            again.Code.ShouldBe(MigrateCommand.Succeeded);
            (await VersionAsync(database, testToken).ConfigureAwait(true)).ShouldBe(MigrationRegistry.Head);
            (reverted.Output + again.Output).ShouldNotContain("Password");
        }
    }

    private static async Task<(int Code, string Output)> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await MigrateCommand.RunAsync(args, output, error, cancellationToken).ConfigureAwait(false);
        return (code, output.ToString() + error);
    }

    private static async Task<int> VersionAsync(ThrowawayDatabase database, CancellationToken cancellationToken)
    {
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource(PostgresRole.Migrator);
        await using (dataSource.ConfigureAwait(false))
        {
            return await new MigrationRunner(dataSource).GetCurrentVersionAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
