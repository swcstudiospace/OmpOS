using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>Verifies manual runs through the API: the cloud agent request that is built, the history, and the concurrency and hourly guards.</summary>
public sealed class AutomationRunEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Run_now_returns_202_starts_a_cloud_agent_for_the_owner_and_records_a_launched_run()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
        var accepted = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        accepted.GetProperty("status").GetString().ShouldBe("running");
        accepted.GetProperty("trigger").GetString().ShouldBe("manual");
        var runs = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        var run = runs.EnumerateArray().Single();
        run.GetProperty("runId").GetString().ShouldBe(accepted.GetProperty("runId").GetString());
        run.GetProperty("status").GetString().ShouldBe("launched");
        run.GetProperty("externalId").GetString().ShouldStartWith("agent-", Case.Sensitive);
        run.GetProperty("durationSeconds").GetDouble().ShouldBeGreaterThanOrEqualTo(0);

        var launch = host.Launcher.Launches.ShouldHaveSingleItem();
        launch.Owner.ShouldBe(AutomationApiHost.Alice);
        launch.Key.ShouldBe(run.GetProperty("runId").GetString());
        launch.Request.Name.ShouldBe("Nightly review");
        launch.Request.Repositories!.Single().Address.ShouldBe("https://github.com/acme/widgets");
        launch.Request.SecretRefs.ShouldBe(["NPM_TOKEN"]);
        launch.Prompt.ShouldStartWith("Review the changes of the last day.", Case.Sensitive);
        launch.Prompt.ShouldContain("Trigger: Run now.", Case.Sensitive);
        launch.Prompt.ShouldContain("pull_request_create", Case.Sensitive);
    }

    [Fact]
    public async Task A_disabled_automation_can_still_be_run_by_hand()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(AutomationApiHost.CreateBody(enabled: false), cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task A_failing_launch_is_a_failed_run_with_the_launcher_message_and_no_secret()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Launcher.FailWith = "The cloud agent was refused (agent_limit_reached).";
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        var secret = created.GetProperty("webhookSecret").GetString()!;
        using var client = host.Client();
        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);

        var runs = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);

        var run = runs.EnumerateArray().Single();
        run.GetProperty("status").GetString().ShouldBe("failed");
        run.GetProperty("error").GetString().ShouldBe("The cloud agent was refused (agent_limit_reached).");
        runs.ToString().ShouldNotContain(secret, Case.Sensitive);
    }

    [Fact]
    public async Task A_second_run_while_the_first_is_going_is_a_409_and_the_attempt_is_in_the_history_as_skipped()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Launcher.Hold = true;
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        using var first = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
        using var second = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AutomationApiHost.ProblemCodeAsync(second, Token).ConfigureAwait(true)).ShouldBe("automation_busy");
        using var history = await client.GetAsync(new Uri($"/automations/{id}/runs", UriKind.Relative), Token).ConfigureAwait(true);
        var runs = (await AutomationApiHost.ReadAsync(history, Token).ConfigureAwait(true)).GetProperty("runs");
        runs.GetArrayLength().ShouldBe(2);
        runs[0].GetProperty("status").GetString().ShouldBe("skipped");
        runs[1].GetProperty("status").GetString().ShouldBe("running");
        host.Launcher.Release();
        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_hourly_cap_is_a_409_with_its_own_code_until_the_hour_passes()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var body = AutomationApiHost.CreateBody().Replace("\"enabled\":true", "\"enabled\":true,\"maxRunsPerHour\":2", StringComparison.Ordinal);
        var created = await host.CreateAsync(body, cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        for (var run = 0; run < 2; run++)
        {
            using var accepted = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
            accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        }

        using var capped = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
        capped.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AutomationApiHost.ProblemCodeAsync(capped, Token).ConfigureAwait(true)).ShouldBe("automation_hourly_cap");

        host.Clock.Advance(TimeSpan.FromMinutes(61));
        using var again = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task The_history_is_newest_first_and_shows_the_last_run_on_the_automation()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();
        var runIds = new List<string>();
        for (var run = 0; run < 3; run++)
        {
            using var accepted = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/runs", null, Token).ConfigureAwait(true);
            runIds.Add((await AutomationApiHost.ReadAsync(accepted, Token).ConfigureAwait(true)).GetProperty("runId").GetString()!);
            _ = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
            host.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var runs = await host.SettledRunsAsync(id, cancellationToken: Token).ConfigureAwait(true);
        using var get = await client.GetAsync(new Uri($"/automations/{id}", UriKind.Relative), Token).ConfigureAwait(true);
        var automation = await AutomationApiHost.ReadAsync(get, Token).ConfigureAwait(true);

        runs.EnumerateArray().Select(static run => run.GetProperty("runId").GetString()).ShouldBe(runIds.AsEnumerable().Reverse());
        automation.GetProperty("lastRun").GetProperty("runId").GetString().ShouldBe(runIds[2]);
    }
}
