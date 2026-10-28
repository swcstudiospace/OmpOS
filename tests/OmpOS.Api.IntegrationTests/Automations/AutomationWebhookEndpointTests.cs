using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Automations;

/// <summary>
/// Verifies the public webhook receiver: anonymous access guarded by the signature, the one uniform failure, the size cap, replay and event
/// filters, the rate limit, and the canary that a secret or a signature never reaches a response or a log line.
/// </summary>
public sealed class AutomationWebhookEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(string Id, string Secret)> CreateHookAsync(AutomationApiHost host, string triggers = """[{"kind":"webhook"}]""", bool enabled = true)
    {
        var created = await host.CreateAsync(AutomationApiHost.CreateBody(triggers, enabled), cancellationToken: Token).ConfigureAwait(false);
        return (created.GetProperty("automation").GetProperty("id").GetString()!, created.GetProperty("webhookSecret").GetString()!);
    }

    [Fact]
    public async Task A_correctly_signed_delivery_needs_no_login_starts_a_run_and_returns_202_with_its_id()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var request = host.GenericDelivery(id, secret, "{\"deploy\":\"v2\"}");

        using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);
        var body = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        body.GetProperty("status").GetString().ShouldBe("accepted");
        var runs = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        var run = runs.EnumerateArray().Single();
        run.GetProperty("runId").GetString().ShouldBe(body.GetProperty("runId").GetString());
        run.GetProperty("trigger").GetString().ShouldBe("webhook");
        host.Launcher.Launches.ShouldHaveSingleItem().Prompt.ShouldContain("{\"deploy\":\"v2\"}", Case.Sensitive);
    }

    [Fact]
    public async Task Every_authentication_failure_is_the_same_401()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        var cronOnly = await host.CreateAsync(AutomationApiHost.CreateBody("""[{"kind":"cron","cron":"0 9 * * *"}]"""), cancellationToken: Token).ConfigureAwait(true);
        var cronId = cronOnly.GetProperty("automation").GetProperty("id").GetString()!;
        using var anonymous = host.Client(owner: null);
        var attempts = new List<HttpRequestMessage>
        {
            host.GenericDelivery("aut_00000000000000000000000000000000", secret),
            host.GenericDelivery(cronId, secret),
            host.GenericDelivery(id, "whsec_wrong_secret"),
            host.GenericDelivery(id, secret, sentAt: host.Clock.GetUtcNow().AddHours(-1)),
            AutomationApiHost.GitHubDelivery(id, secret, "push", "{}"),
            new HttpRequestMessage(HttpMethod.Post, new Uri($"/hooks/automations/{id}", UriKind.Relative)) { Content = new StringContent("{}", Encoding.UTF8, "application/json") },
            new HttpRequestMessage(HttpMethod.Post, new Uri($"/hooks/automations/{id}", UriKind.Relative)),
        };
        try
        {
            var seen = new List<string>();
            foreach (var attempt in attempts)
            {
                using var response = await anonymous.SendAsync(attempt, Token).ConfigureAwait(true);

                response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
                var problem = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);
                seen.Add($"{problem.GetProperty("type").GetString()}|{problem.GetProperty("title").GetString()}|{problem.GetProperty("detail").GetString()}|{problem.GetProperty("status").GetInt32()}");
            }

            seen.Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
            seen[0].ShouldStartWith("/problems/webhook_unauthorized|", Case.Sensitive);
        }
        finally
        {
            foreach (var attempt in attempts)
            {
                attempt.Dispose();
            }
        }

        host.Launcher.Launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_replayed_delivery_is_a_409_and_starts_no_second_run()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var first = host.GenericDelivery(id, secret);
        using var firstResponse = await anonymous.SendAsync(first, Token).ConfigureAwait(true);
        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);

        using var replay = host.GenericDelivery(id, secret);
        using var replayResponse = await anonymous.SendAsync(replay, Token).ConfigureAwait(true);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AutomationApiHost.ProblemCodeAsync(replayResponse, Token).ConfigureAwait(true)).ShouldBe("webhook_replayed");
        host.Launcher.Launches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_body_over_256_KiB_is_a_413_before_any_authentication_and_a_body_at_the_cap_is_served()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var atCap = host.GenericDelivery(id, secret, new string('x', 256 * 1024));
        using var overCap = host.GenericDelivery(id, secret, new string('x', (256 * 1024) + 1));
        using var overCapUnknown = host.GenericDelivery("aut_unknown", secret, new string('x', (256 * 1024) + 1));

        using var served = await anonymous.SendAsync(atCap, Token).ConfigureAwait(true);
        using var refused = await anonymous.SendAsync(overCap, Token).ConfigureAwait(true);
        using var refusedUnknown = await anonymous.SendAsync(overCapUnknown, Token).ConfigureAwait(true);

        served.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        refused.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        refusedUnknown.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await AutomationApiHost.ProblemCodeAsync(refused, Token).ConfigureAwait(true)).ShouldBe("webhook_too_large");
    }

    [Fact]
    public async Task A_chunked_body_over_the_cap_is_refused_without_a_content_length()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, _) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/hooks/automations/{id}", UriKind.Relative))
        {
            Content = new StreamContent(new MemoryStream(new byte[(256 * 1024) + 10])),
        };
        request.Headers.TransferEncodingChunked = true;
        request.Content.Headers.ContentLength = null;

        using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task A_disabled_automation_answers_an_authentic_delivery_with_ignored_and_runs_nothing()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host, enabled: false).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var request = host.GenericDelivery(id, secret);

        using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("ignored");
        host.Launcher.Launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_github_push_to_a_listed_branch_runs_with_a_summary_and_other_branches_are_ignored()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host, """[{"kind":"webhook","flavour":"github","events":["push"],"branches":["main","release/*"]}]""").ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var main = AutomationApiHost.GitHubDelivery(id, secret, "push", PushPayload("main"), "d-1");
        using var other = AutomationApiHost.GitHubDelivery(id, secret, "push", PushPayload("feature/x"), "d-2");

        using var accepted = await anonymous.SendAsync(main, Token).ConfigureAwait(true);
        using var ignored = await anonymous.SendAsync(other, Token).ConfigureAwait(true);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        ignored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AutomationApiHost.ReadAsync(ignored, Token).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("ignored");
        var runs = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        runs.EnumerateArray().Single().GetProperty("summary").GetString().ShouldBe("push to main by octocat (1 commits)");
        var launch = host.Launcher.Launches.ShouldHaveSingleItem();
        launch.Prompt.ShouldContain("BEGIN UNTRUSTED TRIGGER DATA", Case.Sensitive);
        launch.Prompt.ShouldContain("branch: main", Case.Sensitive);
    }

    [Fact]
    public async Task A_github_ping_is_answered_with_pong_and_a_pull_request_from_a_fork_is_ignored()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host, """[{"kind":"webhook","flavour":"github"}]""").ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var ping = AutomationApiHost.GitHubDelivery(id, secret, "ping", "{\"zen\":\"x\"}", "d-ping");
        using var fork = AutomationApiHost.GitHubDelivery(id, secret, "pull_request", ForkPullRequest, "d-fork");

        using var pong = await anonymous.SendAsync(ping, Token).ConfigureAwait(true);
        using var forked = await anonymous.SendAsync(fork, Token).ConfigureAwait(true);

        pong.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AutomationApiHost.ReadAsync(pong, Token).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("pong");
        forked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AutomationApiHost.ReadAsync(forked, Token).ConfigureAwait(true)).GetProperty("status").GetString().ShouldBe("ignored");
        host.Launcher.Launches.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_authentic_delivery_the_concurrency_guard_refuses_is_a_429()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Launcher.Hold = true;
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        using var first = host.GenericDelivery(id, secret, "{\"n\":1}");
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        using var second = host.GenericDelivery(id, secret, "{\"n\":2}");

        using var accepted = await anonymous.SendAsync(first, Token).ConfigureAwait(true);
        using var refused = await anonymous.SendAsync(second, Token).ConfigureAwait(true);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await AutomationApiHost.ProblemCodeAsync(refused, Token).ConfigureAwait(true)).ShouldBe("automation_limit");
        host.Launcher.Release();
        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_receiver_is_rate_limited_for_every_caller_valid_or_not()
    {
        var host = await AutomationApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Automations:WebhookRequestsPerMinute"] = "3" }, Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        var statuses = new List<HttpStatusCode>();
        string? retryAfter = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = host.GenericDelivery("aut_unknown", "whsec_wrong");
            using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        statuses.Take(3).ShouldAllBe(static status => status == HttpStatusCode.Unauthorized);
        statuses.Skip(3).ShouldAllBe(static status => status == HttpStatusCode.TooManyRequests);
        retryAfter.ShouldBe("60");
    }

    [Fact]
    public async Task No_response_and_no_log_line_ever_holds_the_secret_a_signature_or_the_body()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, secret) = await CreateHookAsync(host).ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);
        var texts = new List<string>();
        var signatures = new List<string>();
        var requests = new List<HttpRequestMessage>
        {
            host.GenericDelivery(id, secret, "{\"canary\":\"body-canary-123\"}"),
            host.GenericDelivery(id, "whsec_wrong_canary", "{\"canary\":\"body-canary-456\"}"),
            host.GenericDelivery(id, secret, "{\"canary\":\"body-canary-789\"}", host.Clock.GetUtcNow().AddDays(-1)),
        };
        try
        {
            foreach (var request in requests)
            {
                signatures.Add(request.Headers.GetValues("X-OmpOS-Signature").Single());
                using var response = await anonymous.SendAsync(request, Token).ConfigureAwait(true);
                texts.Add(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
            }
        }
        finally
        {
            foreach (var request in requests)
            {
                request.Dispose();
            }
        }

        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        texts.AddRange(host.Logs.Messages);
        foreach (var text in texts)
        {
            text.ShouldNotContain(secret, Case.Sensitive);
            text.ShouldNotContain("whsec_wrong_canary", Case.Sensitive);
            text.ShouldNotContain("body-canary", Case.Sensitive);
            foreach (var signature in signatures)
            {
                text.ShouldNotContain(signature, Case.Sensitive);
            }
        }

        host.Logs.Messages.ShouldContain(static message => message.Contains("was not accepted: bad_signature", StringComparison.Ordinal));
        host.Logs.Messages.ShouldContain(static message => message.Contains("was not accepted: stale_timestamp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task When_automations_are_off_no_route_exists_including_the_receiver()
    {
        var host = await AutomationApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Automations:Enabled"] = "false" }, Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var owner = host.Client();
        using var anonymous = host.Client(owner: null);

        using var list = await owner.GetAsync(new Uri("/automations/definitions", UriKind.Relative), Token).ConfigureAwait(true);
        using var create = await AutomationApiHost.SendAsync(owner, HttpMethod.Post, "/automations", AutomationApiHost.CreateBody(), Token).ConfigureAwait(true);
        using var hook = host.GenericDelivery("aut_x", "whsec_x");
        using var hooked = await anonymous.SendAsync(hook, Token).ConfigureAwait(true);

        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        create.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        hooked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private const string ForkPullRequest =
        """{"action":"opened","pull_request":{"number":7,"title":"t","merged":false,"user":{"login":"stranger"},"html_url":"https://github.com/acme/widgets/pull/7","base":{"ref":"main","repo":{"full_name":"acme/widgets"}},"head":{"ref":"x","repo":{"full_name":"stranger/widgets"}}}}""";

    private static string PushPayload(string branch) =>
        $$"""{"ref":"refs/heads/{{branch}}","after":"abc123","deleted":false,"repository":{"full_name":"acme/widgets"},"pusher":{"name":"octocat"},"commits":[{"id":"abc123def456","message":"Fix"}]}""";
}
