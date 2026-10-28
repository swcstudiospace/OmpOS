using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// No new route repeats a prompt body, a steering message or the caller's bearer token, in any response (success or problem) or in any log
/// line. <c>GET /sessions</c> is left out of the response check on purpose: it keeps the stored session shape, which carries the session's own messages.
/// </summary>
public sealed class WebAppDisclosureTests
{
    private const string PromptCanary = "PROMPT-CANARY-7f3a91";
    private const string SteerCanary = "STEER-CANARY-91bc02";

    /// <summary>Across a held run and every new route, neither canary nor the bearer token appears in a response body or a log message.</summary>
    [Fact]
    public async Task NewRoutes_NeverRepeatAPromptASteerMessageOrTheToken()
    {
        var ct = TestContext.Current.CancellationToken;
        using var logs = new CapturingLoggerProvider();
        using var factory = WebAppTestHost.Jwt(WebAppTestHost.HeldAtApproval(), logger: logs);
        var token = JwtTestHost.IssueToken("alice");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var id = await WebAppTestHost.StartHeldRunAsync(client, PromptCanary, ct).ConfigureAwait(true);
        var bodies = new List<string>();

        using (var steer = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{SteerCanary}\",\"mode\":\"send_now\"}}", ct).ConfigureAwait(true))
        {
            steer.StatusCode.ShouldBe(HttpStatusCode.OK);
            bodies.Add(await steer.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        }

        using (var invalid = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{SteerCanary}\",\"mode\":\"sideways\"}}", ct).ConfigureAwait(true))
        {
            invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            bodies.Add(await invalid.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        }

        using (var put = await WebAppTestHost.SendAsync(client, HttpMethod.Put, $"/sessions/{id}/config-options/mode", $"{{\"value\":\"{SteerCanary}\"}}", ct).ConfigureAwait(true))
        {
            bodies.Add(await put.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        }

        foreach (var suffix in new[] { "stages", "context", "card-details", "tool-approvals", "config-options", "commands", "changes" })
        {
            using var response = await client.GetAsync(new Uri($"/sessions/{id}/{suffix}", UriKind.Relative), ct).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            bodies.Add(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        }

        foreach (var path in new[] { "/board", "/workspace", "/source" })
        {
            bodies.Add(await client.GetStringAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(true));
        }

        await factory.Services.GetRequiredService<CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);
        var stream = await SessionClient.ReadStreamAsync(client, "/events", 3, ct, lastEventId: "0").ConfigureAwait(true);
        bodies.AddRange(stream.Select(static item => item.Data));

        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
        foreach (var body in bodies)
        {
            body.ShouldNotContain(PromptCanary, Case.Sensitive);
            body.ShouldNotContain(SteerCanary, Case.Sensitive);
            body.ShouldNotContain(token, Case.Sensitive);
        }

        foreach (var message in logs.Messages)
        {
            message.ShouldNotContain(PromptCanary, Case.Sensitive);
            message.ShouldNotContain(SteerCanary, Case.Sensitive);
            message.ShouldNotContain(token, Case.Sensitive);
        }

        using var first = JsonDocument.Parse(bodies[0]);
        first.RootElement.GetProperty("delivery").GetString().ShouldBe("delivered");
    }
}
