using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>Who may do what: the owner boundary on every route, the request validation, the per-owner caps with their problem details, and the idempotency of creation.</summary>
public sealed class CloudAgentAccessTests
{
    /// <summary>An agent of another owner reads as absent on every route that names it, with the same problem as an agent that never existed.</summary>
    [Fact]
    public async Task AnotherOwnersAgent_IsNotFoundOnEveryRoute()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        using var bob = host.Client(CloudApiHost.Bob);

        var attempts = new List<(HttpMethod Method, string Path, string? Body)>
        {
            (HttpMethod.Get, $"/cloud/agents/{id}", null),
            (HttpMethod.Post, $"/cloud/agents/{id}/prompts", "{\"prompt\":\"hi\"}"),
            (HttpMethod.Post, $"/cloud/agents/{id}/cancel", null),
            (HttpMethod.Post, $"/cloud/agents/{id}/stop", null),
            (HttpMethod.Post, $"/cloud/agents/{id}/wake", null),
            (HttpMethod.Delete, $"/cloud/agents/{id}", null),
            (HttpMethod.Get, $"/cloud/agents/{id}/events", null),
            (HttpMethod.Post, $"/cloud/agents/{id}/pull-request", "{\"title\":\"t\"}"),
        };
        foreach (var (method, path, body) in attempts)
        {
            using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
            if (body is not null)
            {
                request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await bob.SendAsync(request, ct).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
            (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe("agent_not_found");
        }

        using var missing = await bob.GetAsync(new Uri("/cloud/agents/does-not-exist", UriKind.Relative), ct).ConfigureAwait(true);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var list = await bob.GetAsync(new Uri("/cloud/agents", UriKind.Relative), ct).ConfigureAwait(true);
        (await CloudApiHost.ReadAsync(list, ct).ConfigureAwait(true)).GetProperty("agents").GetArrayLength().ShouldBe(0);
        host.Environment.Removed.ShouldBeEmpty();
        (await host.WaitForStatusAsync(id, "running", cancellationToken: ct).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("running");
    }

    /// <summary>A request with no token is refused before it reaches any cloud route.</summary>
    [Fact]
    public async Task Unauthenticated_RequestsAreRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var anonymous = host.Client();
        anonymous.DefaultRequestHeaders.Remove(TestOwnerAuthHandler.Header);

        using var list = await anonymous.GetAsync(new Uri("/cloud/agents", UriKind.Relative), ct).ConfigureAwait(true);
        using var limits = await anonymous.GetAsync(new Uri("/cloud/limits", UriKind.Relative), ct).ConfigureAwait(true);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        limits.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Each invalid create request is a 400 with its own stable code, and the problem text never repeats what the caller sent.</summary>
    [Theory]
    [InlineData("{}", "invalid_repositories")]
    [InlineData("{\"repositories\":[]}", "invalid_repositories")]
    [InlineData("{\"repositories\":[{\"url\":\"http://github.com/acme/widgets\"}]}", "invalid_repository")]
    [InlineData("{\"repositories\":[{\"url\":\"https://token123@github.com/acme/widgets\"}]}", "invalid_repository")]
    [InlineData("{\"repositories\":[{\"url\":\"https://evil.example/acme/widgets\"}]}", "invalid_repository")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme\"}]}", "invalid_repository")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"},{\"url\":\"https://github.com/acme/widgets.git\"}]}", "duplicate_repository")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\",\"branch\":\"-bad\"}]}", "invalid_branch")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"idleMinutes\":0}", "invalid_idle_minutes")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"idleMinutes\":99999}", "invalid_idle_minutes")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"environmentId\":\"nope\"}", "unknown_environment")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"secretRefs\":[\"MISSING_ONE\"]}", "unknown_secret")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"secretRefs\":[\"OMPOS_X\"]}", "invalid_secret_ref")]
    [InlineData("{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"}],\"name\":\"" + "NAME_TOO_LONG_0123456789_0123456789_0123456789_0123456789_0123456789_0123456789_0123456789_0123456789_0123456789" + "\"}", "invalid_name")]
    public async Task InvalidCreateRequests_AreRefusedWithAStableCode(string body, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", body, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        text.ShouldContain("/problems/" + code, Case.Sensitive);
        text.ShouldNotContain("token123", Case.Sensitive);
        host.Environment.Fake.ContainersCreated.ShouldBe(0);
    }

    /// <summary>A body that is not JSON, or not JSON at all, is refused before anything is created.</summary>
    [Fact]
    public async Task MalformedBodies_AreRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var notJson = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", "{not json", ct).ConfigureAwait(true);
        using var plain = new StringContent("hello", System.Text.Encoding.UTF8, "text/plain");
        using var wrongType = await client.PostAsync(new Uri("/cloud/agents", UriKind.Relative), plain, ct).ConfigureAwait(true);

        notJson.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        wrongType.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    /// <summary>An owner at the agent cap is refused with a 429 problem; deleting an agent frees a place.</summary>
    [Fact]
    public async Task AgentCap_RefusesTheNextAgentUntilOneIsDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Limits:MaxAgentsPerOwner"] = "2", ["Cloud:Limits:MaxConcurrentRunningPerOwner"] = "2" },
            ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var first = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        _ = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        using var client = host.Client();

        using var refused = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("agent_limit_reached");
        using var bobClient = host.Client(CloudApiHost.Bob);
        using var bobCreate = await CloudApiHost.PostJsonAsync(bobClient, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct).ConfigureAwait(true);
        bobCreate.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var deleted = await client.DeleteAsync(new Uri($"/cloud/agents/{first.GetProperty("id").GetString()}", UriKind.Relative), ct).ConfigureAwait(true);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var allowed = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct).ConfigureAwait(true);
        allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>An owner with the maximum number of running agents is refused a new one and a wake; stopping one frees the slot.</summary>
    [Fact]
    public async Task RunningCap_RefusesAnotherRunningAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Limits:MaxConcurrentRunningPerOwner"] = "1" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var first = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var firstId = first.GetProperty("id").GetString()!;
        using var client = host.Client();

        using var refused = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("running_limit_reached");

        using var stop = await host.PostAsync($"/cloud/agents/{firstId}/stop", cancellationToken: ct).ConfigureAwait(true);
        stop.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        using var wake = await host.PostAsync($"/cloud/agents/{firstId}/wake", cancellationToken: ct).ConfigureAwait(true);
        wake.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await CloudApiHost.ProblemCodeAsync(wake, ct).ConfigureAwait(true)).ShouldBe("running_limit_reached");
        second.GetProperty("status").GetString().ShouldBe("running");
    }

    /// <summary>The limits route reports the caps and the use of them, per caller.</summary>
    [Fact]
    public async Task Limits_ReportCapsAndUse()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        _ = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        host.Clock.Advance(TimeSpan.FromSeconds(90));
        using var client = host.Client();

        using var response = await client.GetAsync(new Uri("/cloud/limits", UriKind.Relative), ct).ConfigureAwait(true);
        var limits = await CloudApiHost.ReadAsync(response, ct).ConfigureAwait(true);

        limits.GetProperty("maxAgents").GetInt32().ShouldBe(5);
        limits.GetProperty("maxConcurrentRunning").GetInt32().ShouldBe(2);
        limits.GetProperty("agents").GetInt32().ShouldBe(1);
        limits.GetProperty("running").GetInt32().ShouldBe(1);
        limits.GetProperty("defaultIdleMinutes").GetInt32().ShouldBe(15);
        limits.GetProperty("computeSeconds").GetDouble().ShouldBe(90, 1.0);
        using var bob = host.Client(CloudApiHost.Bob);
        using var bobResponse = await bob.GetAsync(new Uri("/cloud/limits", UriKind.Relative), ct).ConfigureAwait(true);
        (await CloudApiHost.ReadAsync(bobResponse, ct).ConfigureAwait(true)).GetProperty("agents").GetInt32().ShouldBe(0);
    }

    /// <summary>The same Idempotency-Key with the same request returns the first agent and creates nothing new.</summary>
    [Fact]
    public async Task IdempotencyKey_ReplaysTheFirstAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();
        var body = CloudApiHost.DefaultCreateBody();

        using var first = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", body, ct, ("Idempotency-Key", "key-1")).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var firstAgent = await CloudApiHost.ReadAsync(first, ct).ConfigureAwait(true);
        using var second = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", body, ct, ("Idempotency-Key", "key-1")).ConfigureAwait(true);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.GetValues("Idempotency-Replayed").ShouldHaveSingleItem().ShouldBe("true");
        (await CloudApiHost.ReadAsync(second, ct).ConfigureAwait(true)).GetProperty("id").GetString().ShouldBe(firstAgent.GetProperty("id").GetString());
        _ = await host.WaitForStatusAsync(firstAgent.GetProperty("id").GetString()!, "running", cancellationToken: ct).ConfigureAwait(true);
        host.Environment.Fake.ContainersCreated.ShouldBe(1);
        using var list = await client.GetAsync(new Uri("/cloud/agents", UriKind.Relative), ct).ConfigureAwait(true);
        (await CloudApiHost.ReadAsync(list, ct).ConfigureAwait(true)).GetProperty("agents").GetArrayLength().ShouldBe(1);
    }

    /// <summary>The same Idempotency-Key with a different request is a 409; another owner may use the same key freely.</summary>
    [Fact]
    public async Task IdempotencyKey_WithADifferentRequestIsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();
        using var first = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct, ("Idempotency-Key", "key-2")).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var different = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody("\"idleMinutes\":3"), ct, ("Idempotency-Key", "key-2")).ConfigureAwait(true);
        using var bobClient = host.Client(CloudApiHost.Bob);
        using var bob = await CloudApiHost.PostJsonAsync(bobClient, "/cloud/agents", CloudApiHost.DefaultCreateBody("\"idleMinutes\":3"), ct, ("Idempotency-Key", "key-2")).ConfigureAwait(true);

        different.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(different, ct).ConfigureAwait(true)).ShouldBe("idempotency_key_reused");
        bob.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>An Idempotency-Key that is empty or uses characters outside visible ASCII is refused.</summary>
    [Fact]
    public async Task IdempotencyKey_MustBeVisibleAscii()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, "/cloud/agents", CloudApiHost.DefaultCreateBody(), ct, ("Idempotency-Key", "has space")).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe("invalid_idempotency_key");
    }

    /// <summary>Prompts are validated: empty, too long, and the wrong content type are refused with the status the session endpoints use.</summary>
    [Fact]
    public async Task Prompt_IsValidated()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:MaxPromptLength"] = "20" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;

        using var empty = await host.PromptAsync(id, "   ", cancellationToken: ct).ConfigureAwait(true);
        using var tooLong = await host.PromptAsync(id, new string('x', 21), cancellationToken: ct).ConfigureAwait(true);

        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        host.Worker.Prompts.ShouldBeEmpty();
    }

    /// <summary>The list shows the caller's agents newest first.</summary>
    [Fact]
    public async Task List_ShowsTheCallersAgentsNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var first = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        using var client = host.Client();

        using var response = await client.GetAsync(new Uri("/cloud/agents", UriKind.Relative), ct).ConfigureAwait(true);
        var agents = (await CloudApiHost.ReadAsync(response, ct).ConfigureAwait(true)).GetProperty("agents");

        agents.GetArrayLength().ShouldBe(2);
        agents[0].GetProperty("id").GetString().ShouldBe(second.GetProperty("id").GetString());
        agents[1].GetProperty("id").GetString().ShouldBe(first.GetProperty("id").GetString());
    }
}
