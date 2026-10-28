using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Xunit;
using static Autonogrammer.Sessions.Postgres.Tests.SessionSamples;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>Dependency-injection behaviour of <see cref="ServiceCollectionExtensions.AddPostgresSessionStore"/>.</summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresServiceRegistrationTests(PostgresServerFixture server)
{
    /// <summary>Registration itself never throws, even when the connection string is missing.</summary>
    [Fact]
    public void RegistrationDoesNotThrowWhenConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Should.NotThrow(() => services.AddPostgresSessionStore(configuration));
    }

    /// <summary>Resolving the data source with no connection string throws, and the message names the configuration key.</summary>
    [Fact]
    public async Task ResolvingDataSourceWithoutConnectionStringNamesTheKey()
    {
        var services = new ServiceCollection();
        services.AddPostgresSessionStore(new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider();
        try
        {
            var error = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<NpgsqlDataSource>());

            error.Message.ShouldContain(ServiceCollectionExtensions.ConnectionStringKey);
        }
        finally
        {
            await provider.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A blank connection string is treated as missing, and the message names the configuration key.</summary>
    [Fact]
    public async Task ResolvingDataSourceWithBlankConnectionStringNamesTheKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [ServiceCollectionExtensions.ConnectionStringKey] = "   " })
            .Build();
        var services = new ServiceCollection();
        services.AddPostgresSessionStore(configuration);
        var provider = services.BuildServiceProvider();
        try
        {
            var error = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<NpgsqlDataSource>());

            error.Message.ShouldContain(ServiceCollectionExtensions.ConnectionStringKey);
        }
        finally
        {
            await provider.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// A configured container resolves a repository that works against the database, puts the tables in the <c>autonogrammer</c>
    /// schema through its search path, registers no ISessionRepository, and reuses one instance.
    /// </summary>
    [Fact]
    public async Task ConfiguredContainerResolvesAWorkingRepository()
    {
        var ct = TestContext.Current.CancellationToken;
        // The data source sets its own search path, so this test needs a database of its own rather than a schema.
        var database = await PostgresTestDatabase.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [ServiceCollectionExtensions.ConnectionStringKey] = database.ConnectionString })
                .Build();
            var services = new ServiceCollection();
            services.AddPostgresSessionStore(configuration);
            var provider = services.BuildServiceProvider();
            try
            {
                var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
                await PostgresSessionSchema.EnsureCreatedAsync(dataSource, ct).ConfigureAwait(true);
                var repository = provider.GetRequiredService<PostgresSessionRepository>();
                await repository.SaveAsync(Sample("s-1"), ct).ConfigureAwait(true);
                await AppendDeltasAsync(repository, "s-1", 0, 2, ct).ConfigureAwait(true);

                var loaded = await repository.GetAsync("s-1", ct).ConfigureAwait(true);
                loaded.ShouldNotBeNull();
                loaded.Events.Select(static evt => evt.Seq).ToArray().ShouldBe([1L, 2L]);
                provider.GetRequiredService<PostgresSessionRepository>().ShouldBeSameAs(repository);
                provider.GetService<ISessionRepository>().ShouldBeNull();

                using var location = dataSource.CreateCommand("SELECT to_regclass('autonogrammer.session_events') IS NOT NULL");
                (await location.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe(true);
            }
            finally
            {
                await provider.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
        }
    }
}
