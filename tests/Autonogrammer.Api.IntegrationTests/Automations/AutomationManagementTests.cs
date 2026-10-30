using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Automations;

/// <summary>Verifies the management routes: create, read, replace, enable, delete, ownership, validation and the one-time secret.</summary>
public sealed class AutomationManagementTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_returns_201_with_a_location_the_next_run_a_webhook_path_and_the_secret_once()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, "/automations", AutomationApiHost.CreateBody(), Token).ConfigureAwait(true);
        var body = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var automation = body.GetProperty("automation");
        var id = automation.GetProperty("id").GetString()!;
        response.Headers.Location!.ToString().ShouldBe("/automations/" + id);
        automation.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        automation.GetProperty("nextRunAt").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        automation.GetProperty("webhookPath").GetString().ShouldBe("/hooks/automations/" + id);
        automation.GetProperty("hasWebhookSecret").GetBoolean().ShouldBeTrue();
        automation.GetProperty("allowedTools").EnumerateArray().Select(static tool => tool.GetString()).ShouldBe(["pull_request_create"]);
        automation.GetProperty("maxConcurrentRuns").GetInt32().ShouldBe(1);
        automation.GetProperty("target").GetProperty("kind").GetString().ShouldBe("cloud");
        automation.GetProperty("target").GetProperty("secretRefs").EnumerateArray().Select(static name => name.GetString()).ShouldBe(["NPM_TOKEN"]);
        body.GetProperty("webhookSecret").GetString().ShouldStartWith("whsec_", Case.Sensitive);
        automation.TryGetProperty("webhookSecret", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_cron_only_automation_has_no_webhook_path_and_no_secret()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var created = await host.CreateAsync(AutomationApiHost.CreateBody("""[{"kind":"cron","cron":"*/5 * * * *"}]"""), cancellationToken: Token).ConfigureAwait(true);

        created.TryGetProperty("webhookSecret", out _).ShouldBeFalse();
        created.GetProperty("automation").TryGetProperty("webhookPath", out _).ShouldBeFalse();
        created.GetProperty("automation").GetProperty("hasWebhookSecret").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_new_automation_is_off_unless_the_request_turns_it_on_and_an_off_automation_has_no_next_run()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);

        var created = await host.CreateAsync("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"0 9 * * *"}],"target":{"repositories":[{"address":"https://github.com/acme/widgets"}]}}""", cancellationToken: Token).ConfigureAwait(true);

        var automation = created.GetProperty("automation");
        automation.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        automation.TryGetProperty("nextRunAt", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Get_and_list_show_the_automation_without_ever_showing_the_secret()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var secret = created.GetProperty("webhookSecret").GetString()!;
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        var surfaces = new List<string>();
        foreach (var path in new[] { $"/automations/{id}", "/automations/definitions", $"/automations/{id}/runs" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
            surfaces.Add(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        }

        using var listResponse = await client.GetAsync(new Uri("/automations/definitions", UriKind.Relative), Token).ConfigureAwait(true);
        var list = (await AutomationApiHost.ReadAsync(listResponse, Token).ConfigureAwait(true)).GetProperty("automations");
        list.GetArrayLength().ShouldBe(1);
        list[0].GetProperty("id").GetString().ShouldBe(id);
        list[0].GetProperty("hasWebhookSecret").GetBoolean().ShouldBeTrue();
        foreach (var text in surfaces)
        {
            text.ShouldNotContain(secret, Case.Sensitive);
            text.ShouldNotContain("whsec_", Case.Sensitive);
        }

        host.Logs.Messages.ShouldAllBe(message => !message.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Another_owners_automation_reads_as_absent_on_every_route()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var bob = host.Client(AutomationApiHost.Bob);

        var attempts = new (HttpMethod Method, string Path, string? Body)[]
        {
            (HttpMethod.Get, $"/automations/{id}", null),
            (HttpMethod.Put, $"/automations/{id}", AutomationApiHost.CreateBody()),
            (HttpMethod.Delete, $"/automations/{id}", null),
            (HttpMethod.Put, $"/automations/{id}/enabled", """{"enabled":false}"""),
            (HttpMethod.Post, $"/automations/{id}/runs", null),
            (HttpMethod.Get, $"/automations/{id}/runs", null),
            (HttpMethod.Post, $"/automations/{id}/webhook-secret", null),
            (HttpMethod.Get, "/automations/aut_does_not_exist", null),
        };
        foreach (var (method, path, body) in attempts)
        {
            using var response = await AutomationApiHost.SendAsync(bob, method, path, body, Token).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{method} {path}");
            (await AutomationApiHost.ProblemCodeAsync(response, Token).ConfigureAwait(true)).ShouldBe("automation_not_found");
        }

        using var list = await bob.GetAsync(new Uri("/automations/definitions", UriKind.Relative), Token).ConfigureAwait(true);
        (await AutomationApiHost.ReadAsync(list, Token).ConfigureAwait(true)).GetProperty("automations").GetArrayLength().ShouldBe(0);
        using var alice = host.Client();
        using var still = await alice.GetAsync(new Uri($"/automations/{id}", UriKind.Relative), Token).ConfigureAwait(true);
        still.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Management_routes_need_authentication()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var anonymous = host.Client(owner: null);

        foreach (var (method, path, body) in new (HttpMethod, string, string?)[]
        {
            (HttpMethod.Post, "/automations", AutomationApiHost.CreateBody()),
            (HttpMethod.Get, "/automations/definitions", null),
            (HttpMethod.Get, "/automations/cron-preview?expression=*", null),
            (HttpMethod.Get, "/automations/aut_x", null),
        })
        {
            using var response = await AutomationApiHost.SendAsync(anonymous, method, path, body, Token).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, $"{method} {path}");
        }
    }

    [Theory]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"61 * * * *"}],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "cron")]
    [InlineData("""{"name":"","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "name")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "triggers")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"sms"}],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "triggers")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"webhook","flavour":"gitlab"}],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "webhook")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"kind":"mainframe","repositories":[{"address":"https://github.com/acme/w"}]}}""", "target")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"repositories":[]}}""", "repositories")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"repositories":[{"address":"http://github.com/acme/w"}]}}""", "repositories")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"repositories":[{"address":"https://github.com/acme/w"}],"secretRefs":["1bad"]}}""", "secrets")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"allowedTools":["shell"],"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "tools")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"maxRunsPerHour":0,"target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "limits")]
    [InlineData("""{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"timeZone":"Mars/Olympus","target":{"repositories":[{"address":"https://github.com/acme/w"}]}}""", "timeZone")]
    public async Task An_invalid_definition_is_a_400_that_names_the_field(string body, string field)
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, "/automations", body, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);
        problem.GetProperty("type").GetString().ShouldBe("/problems/invalid_automation");
        problem.GetProperty("detail").GetString()!.Split('\n').ShouldContain(line => line.StartsWith(field + ": ", StringComparison.Ordinal));
        using var list = await client.GetAsync(new Uri("/automations/definitions", UriKind.Relative), Token).ConfigureAwait(true);
        (await AutomationApiHost.ReadAsync(list, Token).ConfigureAwait(true)).GetProperty("automations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_local_target_is_refused_because_this_api_cannot_run_it()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(
            client,
            HttpMethod.Post,
            "/automations",
            """{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"kind":"local"}}""",
            Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true)).GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("cannot run local automations", Case.Sensitive);
    }

    [Fact]
    public async Task The_validation_message_never_repeats_a_repository_url_with_a_credential()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(
            client,
            HttpMethod.Post,
            "/automations",
            """{"name":"n","prompt":"p","triggers":[{"kind":"cron","cron":"* * * * *"}],"target":{"repositories":[{"address":"https://user:hunter2@github.com/acme/w"}]}}""",
            Token).ConfigureAwait(true);

        (await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldNotContain("hunter2", Case.Insensitive);
    }

    [Fact]
    public async Task A_body_that_is_not_json_or_not_an_object_is_refused()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var wrongType = new HttpRequestMessage(HttpMethod.Post, new Uri("/automations", UriKind.Relative)) { Content = new StringContent("name=x", System.Text.Encoding.UTF8, "text/plain") };
        using var wrongTypeResponse = await client.SendAsync(wrongType, Token).ConfigureAwait(true);
        using var malformed = await AutomationApiHost.SendAsync(client, HttpMethod.Post, "/automations", "{ nope", Token).ConfigureAwait(true);
        using var array = await AutomationApiHost.SendAsync(client, HttpMethod.Post, "/automations", "[]", Token).ConfigureAwait(true);

        wrongTypeResponse.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        array.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Replace_changes_the_definition_keeps_the_secret_and_returns_none()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Put, $"/automations/{id}", AutomationApiHost.CreateBody().Replace("Nightly review", "Weekly review", StringComparison.Ordinal), Token).ConfigureAwait(true);
        var body = await AutomationApiHost.ReadAsync(response, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("automation").GetProperty("name").GetString().ShouldBe("Weekly review");
        body.GetProperty("automation").GetProperty("id").GetString().ShouldBe(id);
        body.GetProperty("automation").GetProperty("hasWebhookSecret").GetBoolean().ShouldBeTrue();
        body.TryGetProperty("webhookSecret", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Rotating_the_secret_shows_the_new_one_once_and_the_old_one_stops_working()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        var oldSecret = created.GetProperty("webhookSecret").GetString()!;
        using var client = host.Client();

        using var rotate = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{id}/webhook-secret", null, Token).ConfigureAwait(true);
        var newSecret = (await AutomationApiHost.ReadAsync(rotate, Token).ConfigureAwait(true)).GetProperty("webhookSecret").GetString()!;

        rotate.StatusCode.ShouldBe(HttpStatusCode.OK);
        newSecret.ShouldNotBe(oldSecret, StringComparer.Ordinal);
        using var anonymous = host.Client(owner: null);
        using var withOld = host.GenericDelivery(id, oldSecret);
        using var withNew = host.GenericDelivery(id, newSecret);
        (await anonymous.SendAsync(withOld, Token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.SendAsync(withNew, Token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Rotating_an_automation_without_a_webhook_is_a_400()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(AutomationApiHost.CreateBody("""[{"kind":"cron","cron":"0 9 * * *"}]"""), cancellationToken: Token).ConfigureAwait(true);
        using var client = host.Client();

        using var response = await AutomationApiHost.SendAsync(client, HttpMethod.Post, $"/automations/{created.GetProperty("automation").GetProperty("id").GetString()}/webhook-secret", null, Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Enabling_and_disabling_changes_the_next_run()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        using var client = host.Client();

        using var off = await AutomationApiHost.SendAsync(client, HttpMethod.Put, $"/automations/{id}/enabled", """{"enabled":false}""", Token).ConfigureAwait(true);
        var offBody = await AutomationApiHost.ReadAsync(off, Token).ConfigureAwait(true);
        using var on = await AutomationApiHost.SendAsync(client, HttpMethod.Put, $"/automations/{id}/enabled", """{"enabled":true}""", Token).ConfigureAwait(true);
        var onBody = await AutomationApiHost.ReadAsync(on, Token).ConfigureAwait(true);

        offBody.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        offBody.TryGetProperty("nextRunAt", out _).ShouldBeFalse();
        onBody.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        onBody.GetProperty("nextRunAt").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Delete_removes_the_automation_and_its_webhook_stops_accepting_deliveries()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var created = await host.CreateAsync(cancellationToken: Token).ConfigureAwait(true);
        var id = created.GetProperty("automation").GetProperty("id").GetString()!;
        var secret = created.GetProperty("webhookSecret").GetString()!;
        using var client = host.Client();

        using var deleted = await AutomationApiHost.SendAsync(client, HttpMethod.Delete, $"/automations/{id}", null, Token).ConfigureAwait(true);
        using var again = await AutomationApiHost.SendAsync(client, HttpMethod.Delete, $"/automations/{id}", null, Token).ConfigureAwait(true);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var anonymous = host.Client(owner: null);
        using var delivery = host.GenericDelivery(id, secret);
        (await anonymous.SendAsync(delivery, Token).ConfigureAwait(true)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_owner_may_not_create_more_than_fifty_automations()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        for (var index = 0; index < 50; index++)
        {
            _ = await host.CreateAsync(AutomationApiHost.CreateBody("""[{"kind":"cron","cron":"0 9 * * *"}]"""), cancellationToken: Token).ConfigureAwait(true);
        }

        using var client = host.Client();
        using var over = await AutomationApiHost.SendAsync(client, HttpMethod.Post, "/automations", AutomationApiHost.CreateBody(), Token).ConfigureAwait(true);

        over.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _ = await host.CreateAsync(owner: AutomationApiHost.Bob, cancellationToken: Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task Cron_preview_lists_the_next_occurrences_and_explains_a_bad_expression_or_zone()
    {
        var host = await AutomationApiHost.StartAsync(cancellationToken: Token).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var good = await client.GetAsync(new Uri("/automations/cron-preview?expression=0%209%20*%20*%20*&count=3", UriKind.Relative), Token).ConfigureAwait(true);
        using var bad = await client.GetAsync(new Uri("/automations/cron-preview?expression=61%20*%20*%20*%20*", UriKind.Relative), Token).ConfigureAwait(true);
        using var zone = await client.GetAsync(new Uri("/automations/cron-preview?expression=*%20*%20*%20*%20*&timeZone=Mars/Olympus", UriKind.Relative), Token).ConfigureAwait(true);
        using var many = await client.GetAsync(new Uri("/automations/cron-preview?expression=*%20*%20*%20*%20*&count=500", UriKind.Relative), Token).ConfigureAwait(true);

        var goodBody = await AutomationApiHost.ReadAsync(good, Token).ConfigureAwait(true);
        goodBody.GetProperty("nextRuns").EnumerateArray().Select(static run => run.GetDateTimeOffset()).ShouldBe(
        [
            new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero),
        ]);
        goodBody.TryGetProperty("error", out _).ShouldBeFalse();
        var badBody = await AutomationApiHost.ReadAsync(bad, Token).ConfigureAwait(true);
        badBody.GetProperty("nextRuns").GetArrayLength().ShouldBe(0);
        badBody.GetProperty("error").GetString().ShouldNotBeNull().ShouldContain("minute", Case.Sensitive);
        (await AutomationApiHost.ReadAsync(zone, Token).ConfigureAwait(true)).GetProperty("error").GetString().ShouldNotBeNull().ShouldContain("time zone", Case.Sensitive);
        (await AutomationApiHost.ReadAsync(many, Token).ConfigureAwait(true)).GetProperty("nextRuns").GetArrayLength().ShouldBe(20);
    }
}
