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
    private static readonly string[] MultipleKeys = ["semantic-key", "another"];

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
            Content = Json("{\"content\":\"once\",\"ignored\":true}"),
        };
        again.Headers.TryAddWithoutValidation("Idempotency-Key", "mem-1");
        using var replay = await client.SendAsync(again, TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        replay.Content.Headers.ContentType?.MediaType.ShouldBe(created.Content.Headers.ContentType?.MediaType);
        (await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(body);
        await AssertExportContainsOnceAsync(client, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Validation and semantic comparison happen before replay, and claims are account scoped.</summary>
    [Fact]
    public async Task KeyedRequestsValidateAndIsolateSemanticPayloads()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var alice = host.Client("alice");
        using var bob = host.Client("bob");
        var token = TestContext.Current.CancellationToken;
        await AssertSemanticReplayAsync(alice, token).ConfigureAwait(true);
        await AssertSemanticConflictsAndValidationAsync(alice, token).ConfigureAwait(true);
        await AssertAccountIsolationAndErasureAsync(alice, bob, token).ConfigureAwait(true);
    }

    /// <summary>Every effective field participates in a keyed request's identity.</summary>
    [Theory]
    [InlineData("{\"content\":\"note\",\"topic\":\"different\"}")]
    [InlineData("{\"content\":\"different\",\"topic\":\"topic\"}")]
    [InlineData("{\"content\":\"note\",\"topic\":\"topic\",\"category\":\"different\"}")]
    [InlineData("{\"content\":\"note\",\"topic\":\"topic\",\"projectId\":\"different\"}")]
    [InlineData("{\"content\":\"note\",\"topic\":\"topic\",\"useAccountMemory\":true}")]
    [InlineData("{\"content\":\"note\",\"topic\":\"topic\",\"sourceSession\":\"different\"}")]
    public async Task ChangedEffectiveFieldConflicts(string changed)
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var client = host.Client("alice");
        var token = TestContext.Current.CancellationToken;
        using var created = await KeyedAsync(client, "{\"content\":\"note\",\"topic\":\"topic\"}", token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var conflict = await KeyedAsync(client, changed, token).ConfigureAwait(true);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>Pause is checked before replay, and invalid headers cannot replay a saved entry.</summary>
    [Fact]
    public async Task KeyedReplayHonorsPauseAndHeaderValidation()
    {
        await using var host = await CoworkModuleHost.StartAsync(projects: false, memory: true).ConfigureAwait(true);
        using var client = host.Client("alice");
        var token = TestContext.Current.CancellationToken;
        using var saved = await KeyedAsync(client, "{\"content\":\"secret\"}", token).ConfigureAwait(true);
        saved.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var pause = await client.PutAsync(new Uri("/cowork/memory/settings", UriKind.Relative), Json("{\"paused\":true}"), token).ConfigureAwait(true);
        pause.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var paused = await KeyedAsync(client, "{\"content\":\"secret\"}", token).ConfigureAwait(true);
        paused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await paused.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldNotContain("secret");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cowork/memory") { Content = Json("{\"content\":\"secret\"}") };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MultipleKeys);
        using var invalid = await client.SendAsync(request, token).ConfigureAwait(true);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task AssertExportContainsOnceAsync(HttpClient client, CancellationToken token)
    {
        using var export = await client.GetAsync(new Uri("/cowork/memory/export", UriKind.Relative), token).ConfigureAwait(true);
        export.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        (await export.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldContain("once");
    }

    private static async Task AssertSemanticReplayAsync(HttpClient alice, CancellationToken token)
    {
        using var created = await KeyedAsync(alice, "{\"topic\":\"t\",\"content\":\"secret\",\"sourceSession\":\"api\"}", token).ConfigureAwait(true);
        using var reordered = await KeyedAsync(alice, "{\"unknown\":123,\"content\":\"secret\",\"topic\":\"t\"}", token).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        reordered.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await reordered.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldBe(await created.Content.ReadAsStringAsync(token).ConfigureAwait(true));
    }

    private static async Task AssertSemanticConflictsAndValidationAsync(HttpClient alice, CancellationToken token)
    {
        using var conflict = await KeyedAsync(alice, "{\"topic\":\"t\",\"content\":\"different\"}", token).ConfigureAwait(true);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conflict.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldNotContain("secret");
        using var changedSession = await KeyedAsync(alice, "{\"topic\":\"t\",\"content\":\"secret\",\"sourceSession\":\"other\"}", token).ConfigureAwait(true);
        changedSession.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var invalid = await KeyedAsync(alice, "{\"content\":", token).ConfigureAwait(true);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var empty = await KeyedAsync(alice, "{\"content\":\"\"}", token).ConfigureAwait(true);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task AssertAccountIsolationAndErasureAsync(HttpClient alice, HttpClient bob, CancellationToken token)
    {
        using var other = await KeyedAsync(bob, "{\"content\":\"bob\"}", token).ConfigureAwait(true);
        other.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await other.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldNotContain("secret");
        using var reset = await alice.PostAsync(new Uri("/cowork/memory/reset", UriKind.Relative), Json("{}"), token).ConfigureAwait(true);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var gone = await KeyedAsync(alice, "{\"topic\":\"t\",\"content\":\"secret\"}", token).ConfigureAwait(true);
        gone.StatusCode.ShouldBe(HttpStatusCode.Gone);
        (await gone.Content.ReadAsStringAsync(token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    private static async Task<HttpResponseMessage> KeyedAsync(HttpClient client, string body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/cowork/memory") { Content = Json(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "semantic-key");
        return await client.SendAsync(request, token).ConfigureAwait(true);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
