#pragma warning disable CA2000 // HttpContent is disposed by the client send.
#pragma warning disable CA2007 // Test assertions use the caller context.
using System.Net;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>Memory routes: the same account boundary as projects, plus caps, pause, reset, delete, and export.</summary>
public sealed class MemoryApiTests
{
    /// <summary>A missing token answers 401.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task MissingToken_Returns401()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var client = host.Client(subject: null);
        using var response = await client.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Another account's entry answers 404.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task CrossAccount_Returns404()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var alice = host.Client("alice");
        using var created = await alice.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"alpha note\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement.GetProperty("id").GetString();
        using var bob = host.Client("bob");
        using var response = await bob.GetAsync(new Uri("/cowork/memory/" + id, UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A body that names an account answers 400.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task OwnerInBody_Returns400()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var response = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"alpha\",\"ownerId\":\"bob\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("owner_not_accepted");
    }

    /// <summary>The memory limiter is per user and answers 429 with Retry-After.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task RateLimit_IsPerUserAndReturns429()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 1).ConfigureAwait(true);
        using var alice = host.Client("alice");
        using var first = await alice.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var second = await alice.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
        using var bob = host.Client("bob");
        using var other = await bob.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Topic, content, and query caps are refused.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task SizeLimits_ReturnCapOrQueryErrors()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var topic = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"topic\":\"" + new string('t', 121) + "\",\"content\":\"ok\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        topic.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await topic.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("MemoryCapReached");
        using var content = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"" + new string('c', 1001) + "\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        content.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var query = await client.PostAsync(new Uri("/cowork/memory/recall", UriKind.Relative), Json("{\"query\":\"" + new string('q', 201) + "\",\"k\":5}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        query.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await query.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("MemoryQueryTooLong");
    }

    /// <summary>The twenty-first save for one session in an hour is refused.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task SessionHourlyCap_ReturnsRateLimited()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        for (var i = 0; i < 20; i++)
        {
            using var saved = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"note " + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\",\"sourceSession\":\"s1\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
            saved.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        using var over = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"one more\",\"sourceSession\":\"s1\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        over.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await over.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("MemoryRateLimited");
    }

    /// <summary>The thousandth-plus entry is refused. Each save uses its own session so the hourly cap does not fire first.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task EntryCap_ReturnsCapReached()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        for (var i = 0; i < 1000; i++)
        {
            using var saved = await client.PostAsync(
                new Uri("/cowork/memory", UriKind.Relative),
                Json("{\"content\":\"n" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\",\"sourceSession\":\"s" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}"),
                TestContext.Current.CancellationToken).ConfigureAwait(true);
            saved.StatusCode.ShouldBe(HttpStatusCode.Created, await saved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        }

        using var over = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"over\",\"sourceSession\":\"over\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        over.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await over.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("MemoryCapReached");
    }

    /// <summary>Pause keeps entries and refuses a later save.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task Pause_KeepsEntriesAndRefusesWrites()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var saved = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"kept\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        saved.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var paused = await client.PutAsync(new Uri("/cowork/memory/settings", UriKind.Relative), Json("{\"enabled\":true,\"paused\":true,\"includeSensitive\":false}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        paused.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var refused = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"later\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("MemoryPaused");
        using var export = await client.GetAsync(new Uri("/cowork/memory/export", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await export.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("kept");
    }

    /// <summary>Reset hard-deletes account and project memory. Settings stay.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task ResetHardDelete_RemovesProjectMemory()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var saved = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"project note\",\"projectId\":\"p1\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        saved.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var settings = await client.PutAsync(new Uri("/cowork/memory/settings", UriKind.Relative), Json("{\"enabled\":true,\"paused\":false,\"includeSensitive\":false}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var reset = await client.PostAsync(new Uri("/cowork/memory/reset", UriKind.Relative), new StringContent(string.Empty), TestContext.Current.CancellationToken).ConfigureAwait(true);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var export = await client.GetAsync(new Uri("/cowork/memory/export", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await export.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotContain("project note");
        using var kept = await client.GetAsync(new Uri("/cowork/memory/settings", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await kept.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("\"enabled\":true");
    }

    /// <summary>One entry can be deleted. Recall still finds the other.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task DeleteOneEntry_LeavesTheOther()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true, memoryPerMinute: 100_000).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var first = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"keep alpha\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var second = await client.PostAsync(new Uri("/cowork/memory", UriKind.Relative), Json("{\"content\":\"drop beta\"}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var id = JsonDocument.Parse(await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).RootElement.GetProperty("id").GetString();
        using var deleted = await client.DeleteAsync(new Uri("/cowork/memory/" + id, UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var recall = await client.PostAsync(new Uri("/cowork/memory/recall", UriKind.Relative), Json("{\"query\":\"alpha\",\"k\":5}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var body = await recall.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        body.ShouldContain("keep alpha");
        body.ShouldNotContain("drop beta");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>Export is JSON and an Idempotency-Key replays the first save.</summary>
    /// <returns>A task that completes when the assertion finishes.</returns>
    [Fact]
    public async Task ExportAndIdempotencyKey_ReplayFirstSave()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var client = host.Client("alice");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cowork/memory")
        {
            Content = Json("{\"content\":\"once\"}"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "mem-1");
        using var created = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var body = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var again = new HttpRequestMessage(HttpMethod.Post, "/cowork/memory")
        {
            Content = Json("{\"content\":\"twice\"}"),
        };
        again.Headers.TryAddWithoutValidation("Idempotency-Key", "mem-1");
        using var replay = await client.SendAsync(again, TestContext.Current.CancellationToken).ConfigureAwait(true);
        (await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(body);
        using var export = await client.GetAsync(new Uri("/cowork/memory/export", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);
        export.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        (await export.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("once");
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
