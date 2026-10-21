using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Guards: fail-fast config, health, OpenAPI freshness, prompt redaction, and end-to-end tracing.
/// </summary>
public sealed class GuardTests
{
    /// <summary>Verifies a missing required key fails startup with the key name in the message.</summary>
    [Fact]
    public void MissingRequiredConfig_FailsStartupNamingKey()
    {
        using var factory = new OmpApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Harness:Adapter"] = string.Empty });

        Exception? caught = null;
        try
        {
            using var client = factory.CreateClient();
        }
#pragma warning disable CA1031 // The fail-fast assertion must observe any startup exception type.
        catch (Exception ex)
        {
            caught = ex;
        }
#pragma warning restore CA1031

        caught.ShouldNotBeNull("startup must fail without Harness:Adapter");
        Flatten(caught).ShouldContain("Harness:Adapter");
    }

    /// <summary>Verifies liveness is healthy.</summary>
    [Fact]
    public async Task Health_Live_Returns200()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("x-correlation-id").ShouldNotBeEmpty();
    }

    /// <summary>Verifies readiness is healthy with the default configuration.</summary>
    [Fact]
    public async Task Health_Ready_Returns200ByDefault()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies the served OpenAPI document equals the committed build-time document.</summary>
    [Fact]
    public async Task OpenApi_ServedEqualsCommitted()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var served = JsonNode.Parse(await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true))!;

        var committedPath = Path.Combine(FindRepoRoot(), "docs", "api", "openapi.json");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(committedPath, testToken).ConfigureAwait(true))!;

        (served["openapi"]?.GetValue<string>() ?? string.Empty).ShouldStartWith("3.1");
        JsonNode.DeepEquals(served, committed).ShouldBeTrue("the served document must equal the committed build-time document");
    }

    /// <summary>Verifies prompt bodies never reach log output unless verbatim logging is enabled.</summary>
    [Fact]
    public async Task Redaction_PromptNeverLogged()
    {
        var provider = new CapturingLoggerProvider();
        using var factory = new OmpApiFactory(loggerProvider: provider);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var marker = $"PROMPTMARKER-{Guid.NewGuid():N}";
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var content = new StringContent($"{{\"prompt\":\"Summarize {marker} please\"}}", Encoding.UTF8, "application/json");
        using var accepted = await client.PostAsync(new Uri($"/sessions/{id}/prompts", UriKind.Relative), content, testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, TimeSpan.FromSeconds(15), testToken).ConfigureAwait(true);

        provider.Messages.ShouldContain(static message => message.Contains("<redacted", StringComparison.Ordinal));
        provider.Messages.Where(message => message.Contains(marker, StringComparison.Ordinal)).ShouldBeEmpty();
    }

    /// <summary>Verifies one trace spans the endpoint and the background harness run.</summary>
    [Fact]
    public async Task Trace_EndToEndSpansShareTraceId()
    {
        var seen = new ConcurrentBag<(string Source, string Name, ActivityTraceId TraceId)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                string.Equals(source.Name, "OmpOS.Api", StringComparison.Ordinal)
                || source.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => seen.Add((activity.Source.Name, activity.OperationName, activity.TraceId)),
        };
        ActivitySource.AddActivityListener(listener);

        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"trace me\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var events = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken).ConfigureAwait(true);
        events.ShouldNotBeEmpty("the stream must terminate so the background span stops");

        // The run span stops after teardown, which can finish after the terminal event reached the client, so wait for it.
        var runSpanDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!seen.Any(static record => string.Equals(record.Name, "harness.run", StringComparison.Ordinal)) && DateTime.UtcNow < runSpanDeadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), testToken).ConfigureAwait(true);
        }

        var group = seen.GroupBy(static record => record.TraceId).FirstOrDefault(static traces => traces.Any(static record => string.Equals(record.Name, "harness.run", StringComparison.Ordinal)));
        group.ShouldNotBeNull("the harness run must share a trace with the request");
        group!.Any(static record => string.Equals(record.Name, "POST /sessions/{id}/prompts", StringComparison.Ordinal)).ShouldBeTrue();
        group.Count().ShouldBeGreaterThanOrEqualTo(3, "server, endpoint, and harness spans share one trace");
    }

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Could not locate OmpOS.slnx walking up from " + AppContext.BaseDirectory)
            : directory.FullName;
    }
}
