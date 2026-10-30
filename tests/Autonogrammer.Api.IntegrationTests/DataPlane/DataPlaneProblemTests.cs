using System.Net;
using System.Text.Json.Nodes;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsOptions = Microsoft.Extensions.Options.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// The single 503 problem of a store outage (D-45, D-64): the mapper, the middleware that writes it and the repository-wide guarantee that the retired
/// coordination problem code appears nowhere.
/// </summary>
public sealed class DataPlaneProblemTests
{
    private const string LeakyHost = "db-internal.example.test";

    /// <summary>Verifies each outage maps to its component, a closed reason and a retry time.</summary>
    /// <param name="component">The lower-case component name of the exception.</param>
    /// <param name="reason">The pipeline reason.</param>
    /// <param name="expectedComponent">The expected component.</param>
    /// <param name="expectedReason">The expected closed reason.</param>
    /// <param name="expectedRetry">The expected retry seconds.</param>
    [Theory]
    [InlineData("postgres", DataPlaneTimeoutReason.CircuitOpen, DataPlaneComponent.Postgres, DataPlaneReason.CircuitOpen, 15)]
    [InlineData("dragonfly", DataPlaneTimeoutReason.CircuitOpen, DataPlaneComponent.Dragonfly, DataPlaneReason.CircuitOpen, 10)]
    [InlineData("ledger", DataPlaneTimeoutReason.CircuitOpen, DataPlaneComponent.Ledger, DataPlaneReason.CircuitOpen, 60)]
    [InlineData("postgres", DataPlaneTimeoutReason.Timeout, DataPlaneComponent.Postgres, DataPlaneReason.Timeout, 5)]
    [InlineData("dragonfly", DataPlaneTimeoutReason.BulkheadFull, DataPlaneComponent.Dragonfly, DataPlaneReason.Timeout, 5)]
    public void Mapper_MapsPipelineFailures(string component, DataPlaneTimeoutReason reason, DataPlaneComponent expectedComponent, DataPlaneReason expectedReason, int expectedRetry)
    {
        var mapped = Mapper().TryMap(new DataPlaneTimeoutException(component, reason), out var problem);

        mapped.ShouldBeTrue();
        problem!.Type.ShouldBe("/problems/data_plane_unavailable");
        problem.Status.ShouldBe(503);
        problem.Component.ShouldBe(expectedComponent);
        problem.Reason.ShouldBe(expectedReason);
        problem.RetryAfterSeconds.ShouldBe(expectedRetry);
    }

    /// <summary>Verifies a PostgreSQL undefined-table failure is a schema-behind outage, even inside a wrapper.</summary>
    [Fact]
    public void Mapper_MapsASchemaThatIsBehind_AnywhereInTheChain()
    {
        var undefinedTable = new PostgresException("relation does not exist", "ERROR", "ERROR", "42P01");
        var wrapped = new InvalidOperationException("wrapper", new AggregateException(undefinedTable));

        Mapper().TryMap(wrapped, out var problem).ShouldBeTrue();

        problem!.Component.ShouldBe(DataPlaneComponent.Postgres);
        problem.Reason.ShouldBe(DataPlaneReason.SchemaBehind);
    }

    /// <summary>Verifies failures that are not data-plane outages are not mapped.</summary>
    [Fact]
    public void Mapper_IgnoresEverythingElse()
    {
        Mapper().TryMap(new InvalidOperationException("boom"), out _).ShouldBeFalse();
        Mapper().TryMap(new PostgresException("duplicate", "ERROR", "ERROR", "23505"), out _).ShouldBeFalse();
        Mapper().TryMap(new DataPlaneTimeoutException("unknown", DataPlaneTimeoutReason.Timeout), out _).ShouldBeFalse();
    }

    /// <summary>Verifies the middleware answers 503 with the problem members, Retry-After and no exception text.</summary>
    [Fact]
    public async Task Middleware_Answers503_WithTheProblem_AndNeverTheExceptionText()
    {
        var testToken = TestContext.Current.CancellationToken;
        var app = await StartAsync(testToken).ConfigureAwait(true);
        await using var scope = app.ConfigureAwait(true);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/outage", UriKind.Relative), testToken).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        var body = JsonNode.Parse(text)!;

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(10));
        body["type"]!.GetValue<string>().ShouldBe("/problems/data_plane_unavailable");
        body["status"]!.GetValue<int>().ShouldBe(503);
        body["component"]!.GetValue<string>().ShouldBe("Dragonfly");
        body["reason"]!.GetValue<string>().ShouldBe("CircuitOpen");
        body["retryAfterSeconds"]!.GetValue<int>().ShouldBe(10);
        text.ShouldNotContain(LeakyHost);
        response.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();
    }

    /// <summary>Verifies other failures pass through the middleware, and so does an outage after the response started.</summary>
    [Fact]
    public async Task Middleware_LeavesOtherFailuresAndStartedResponsesAlone()
    {
        var middleware = new DataPlaneProblemMiddleware(
            static _ => throw new InvalidOperationException("boom"),
            Mapper(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DataPlaneProblemMiddleware>.Instance);
        var outage = new DataPlaneProblemMiddleware(
            static _ => throw new DataPlaneTimeoutException("postgres", DataPlaneTimeoutReason.Timeout),
            Mapper(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DataPlaneProblemMiddleware>.Instance);

        var unrelated = new DefaultHttpContext();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => middleware.InvokeAsync(unrelated)).ConfigureAwait(true);
        unrelated.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        var started = new DefaultHttpContext();
        started.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        _ = await Should.ThrowAsync<DataPlaneTimeoutException>(() => outage.InvokeAsync(started)).ConfigureAwait(true);
        started.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    /// <summary>Verifies the retired coordination problem code exists nowhere in the source, service or test code (R13).</summary>
    [Fact]
    public async Task CoordinationUnavailable_ExistsNowhere()
    {
        var retired = "coordination" + "_unavailable";
        var root = FindRepositoryRoot();
        var hits = new List<string>();
        foreach (var folder in new[] { "src", "services", "tests", "apps" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true);
                if (text.Contains(retired, StringComparison.OrdinalIgnoreCase))
                {
                    hits.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        hits.ShouldBeEmpty();
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private static DataPlaneProblemMapper Mapper() => new(
        MsOptions.Create(new PostgresResilienceOptions()),
        MsOptions.Create(new DragonflyResilienceOptions()),
        MsOptions.Create(new LedgerResilienceOptions()));

    private static async Task<WebApplication> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        _ = builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _ = builder.Services.AddSingleton(Mapper());
        var app = builder.Build();
        _ = app.Use(static (context, next) =>
        {
            context.Response.Headers["x-correlation-id"] = "test-correlation";
            return next(context);
        });
        _ = app.UseMiddleware<DataPlaneProblemMiddleware>();
        _ = app.MapGet("/ok", static () => "ok");
        _ = app.MapGet("/outage", static Task<string> () => throw new InvalidOperationException("Host=" + LeakyHost, new DataPlaneTimeoutException("dragonfly", DataPlaneTimeoutReason.CircuitOpen)));
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return app;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (Autonogrammer.slnx) was not found above the test binaries.");
    }
}
