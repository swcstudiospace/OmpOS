#pragma warning disable CA2000 // HttpContent is disposed by the client send.
#pragma warning disable CA2007 // Test assertions use the caller context.
#pragma warning disable MA0048 // The host helper lives with the project tests.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Autonogrammer.Api.Cowork;
using Autonogrammer.Api.Security;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Sessions.Postgres.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>Project and global-instruction routes on a JWT host: 401, cross-account 404, owner refusal, limits, and idempotent create.</summary>
public sealed class ProjectsApiTests
{
    /// <summary>A missing token answers 401.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task MissingToken_Returns401()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: true).ConfigureAwait(true);
        using var client = host.Client(subject: null);
        using var response = await client.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Another account's project answers 404.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task CrossAccount_Returns404()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false).ConfigureAwait(true);
        using var alice = host.Client("alice");
        using var created = await alice.PutAsync(new Uri("/cowork/projects/p1", UriKind.Relative), Json("{\"id\":\"p1\",\"name\":\"Alpha\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var bob = host.Client("bob");
        using var response = await bob.GetAsync(new Uri("/cowork/projects/p1", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A body that names an account answers 400.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task OwnerInBody_Returns400()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var response = await client.PutAsync(
            new Uri("/cowork/projects/p1", UriKind.Relative),
            Json("{\"id\":\"p1\",\"name\":\"Alpha\",\"owner\":\"bob\"}"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("owner_not_accepted");
    }

    /// <summary>A body that names folders answers 400. The contract has no folders member.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task FoldersInBody_Returns400()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var response = await client.PutAsync(
            new Uri("/cowork/projects/p1", UriKind.Relative),
            Json("{\"id\":\"p1\",\"name\":\"Alpha\",\"folders\":[\"/tmp\"]}"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The project limiter is per user and answers 429 with Retry-After.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task RateLimit_IsPerUserAndReturns429()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false, projectsPerMinute: 1).ConfigureAwait(true);
        using var alice = host.Client("alice");
        using var first = await alice.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var second = await alice.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
        using var bob = host.Client("bob");
        using var other = await bob.GetAsync(new Uri("/cowork/projects", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Name, instruction, link, and global-instruction limits answer 400.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task SizeLimits_Return400()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false, projectsPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var name = await client.PutAsync(new Uri("/cowork/projects/p1", UriKind.Relative), Json("{\"id\":\"p1\",\"name\":\"" + new string('n', 101) + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        name.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var huge = new string('x', (16 * 1024) + 1);
        using var instructions = await client.PutAsync(new Uri("/cowork/projects/p1", UriKind.Relative), Json("{\"id\":\"p1\",\"name\":\"Alpha\",\"instructions\":\"" + huge + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        instructions.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var urls = string.Join(',', Enumerable.Range(0, 21).Select(static i => "\"https://example.test/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\""));
        using var links = await client.PutAsync(new Uri("/cowork/projects/p1", UriKind.Relative), Json("{\"id\":\"p1\",\"name\":\"Alpha\",\"contextUrls\":[" + urls + "]}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        links.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var global = await client.PutAsync(new Uri("/cowork/instructions/global", UriKind.Relative), Json("{\"instructions\":\"" + new string('g', (8 * 1024) + 1) + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        global.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The 200-project cap answers 400.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task ProjectCap_Returns400()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false, projectsPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        for (var i = 0; i < 200; i++)
        {
            using var created = await client.PutAsync(new Uri("/cowork/projects/p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), UriKind.Relative), Json("{\"id\":\"p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\",\"name\":\"N\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
            created.IsSuccessStatusCode.ShouldBeTrue(created.StatusCode.ToString());
        }

        using var over = await client.PutAsync(new Uri("/cowork/projects/over", UriKind.Relative), Json("{\"id\":\"over\",\"name\":\"N\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        over.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>An Idempotency-Key on create replays the first result.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task IdempotencyKey_ReplaysFirstCreate()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: true, memory: false).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var first = new HttpRequestMessage(HttpMethod.Put, "/cowork/projects/p1")
        {
            Content = Json("{\"id\":\"p1\",\"name\":\"First\"}"),
        };
        first.Headers.TryAddWithoutValidation("Idempotency-Key", "create-1");
        using var created = await client.SendAsync(first, TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var second = new HttpRequestMessage(HttpMethod.Put, "/cowork/projects/p1")
        {
            Content = Json("{\"id\":\"p1\",\"name\":\"Second\"}"),
        };
        second.Headers.TryAddWithoutValidation("Idempotency-Key", "create-1");
        using var replay = await client.SendAsync(second, TestContext.Current.CancellationToken).ConfigureAwait(true);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(body);
        body.ShouldContain("First");
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}

/// <summary>JWT test host that maps the Cowork project and memory extensions. Program.cs is not involved.</summary>
internal sealed class CoworkModuleHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly NpgsqlDataSource? _admin;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly string? _schema;

    private CoworkModuleHost(WebApplication app, NpgsqlDataSource? admin, NpgsqlDataSource? dataSource, string? schema)
    {
        _app = app;
        _admin = admin;
        _dataSource = dataSource;
        _schema = schema;
    }

    /// <summary>Starts the host.</summary>
    /// <param name="projects">Whether project routes are enabled.</param>
    /// <param name="memory">Whether memory routes are enabled.</param>
    /// <param name="projectsPerMinute">The project limiter budget.</param>
    /// <param name="memoryPerMinute">The memory limiter budget.</param>
    /// <returns>The running host.</returns>
    public static async Task<CoworkModuleHost> StartAsync(bool projects, bool memory, int projectsPerMinute = 60, int memoryPerMinute = 60)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        _ = builder.WebHost.UseTestServer();
        var settings = JwtTestHost.DevelopmentConfig();
        settings["Cowork:Projects:Enabled"] = projects ? "true" : "false";
        settings["Cowork:Projects:RequestsPerMinute"] = projectsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings["Cowork:Memory:Enabled"] = memory ? "true" : "false";
        settings["Cowork:Memory:RequestsPerMinute"] = memoryPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ = builder.Configuration.AddInMemoryCollection(settings);
        NpgsqlDataSource? admin = null;
        NpgsqlDataSource? dataSource = null;
        string? schema = null;
        if (projects)
        {
            var connection = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException("A run-owned test PostgreSQL connection is required for project API tests.");
            }

            schema = "t_" + Guid.NewGuid().ToString("n");
            admin = NpgsqlDataSource.Create(connection);
            await new MigrationRunner(admin, schema).MigrateAsync(CancellationToken.None).ConfigureAwait(false);
            dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema + ",public" }.ConnectionString);
            builder.Services.AddSingleton(dataSource);
        }

        builder.Services.AddSingleton<IMemoryStore, InMemoryMemoryStore>();
        _ = builder.Services.AddApiAuthentication(builder.Environment);
        _ = builder.Services.AddCoworkProjects(builder.Configuration);
        _ = builder.Services.AddCoworkMemory(builder.Configuration);
        JwtTestHost.InjectStaticMetadata(builder.Services);
        var app = builder.Build();
        _ = app.UseAuthentication();
        _ = app.UseRateLimiter();
        _ = app.UseAuthorization();
        _ = app.UseMiddleware<CoworkDisabledMiddleware>();
        _ = app.MapCoworkProjects();
        _ = app.MapCoworkMemory();
        await app.StartAsync().ConfigureAwait(false);
        return new CoworkModuleHost(app, admin, dataSource, schema);
    }

    /// <summary>Creates a client. A null subject sends no token.</summary>
    /// <param name="subject">The token subject.</param>
    /// <returns>The client.</returns>
    public HttpClient Client(string? subject)
    {
        var client = _app.GetTestClient();
        if (subject is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken(subject));
        }

        return client;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        if (_dataSource is not null) await _dataSource.DisposeAsync().ConfigureAwait(false);
        if (_admin is not null)
        {
            var command = _admin.CreateCommand("DROP SCHEMA IF EXISTS " + _schema + " CASCADE");
            await using (command.ConfigureAwait(false))
            {
                await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            await _admin.DisposeAsync().ConfigureAwait(false);
        }
    }
}
