using System.Net;
using System.Text.Json;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.InMemory;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// <c>POST /sessions/{id}/steer</c> and <c>GET /sessions/{id}/tool-approvals</c>. A message reaches a live engine (delivered or queued), is
/// held when the engine has no live steering, or fails; a session with no turn is 409 <c>steering_unavailable</c>. The body follows the prompt rules
/// (415, 413, 400) and another owner's session is 404.
/// </summary>
public sealed class SteeringEndpointTests
{
    private const string SteerText = "turn left at the fork";

    /// <summary>A send-now message to a live engine is delivered into the turn.</summary>
    [Fact]
    public async Task SendNow_ToALiveEngine_IsDelivered()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{SteerText}\",\"mode\":\"send_now\"}}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("delivery").GetString().ShouldBe("delivered");
        body.RootElement.TryGetProperty("reason", out _).ShouldBeFalse();
        var engine = factory.Services.GetRequiredService<LiveSessionTracker>().TryGetLive(id).ShouldBeOfType<InMemoryOmpSession>();
        engine.SteeredMessages.ShouldBe([SteerText]);
        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
    }

    /// <summary>A queued message to a live engine goes into the follow-up queue and is reported as queued.</summary>
    [Fact]
    public async Task Queue_ToALiveEngine_IsQueued()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{SteerText}\",\"mode\":\"queue\"}}", ct).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("delivery").GetString().ShouldBe("queued");
        factory.Services.GetRequiredService<LiveSessionTracker>().TryGetLive(id).ShouldBeOfType<InMemoryOmpSession>().SteeredMessages.ShouldBe([SteerText]);
        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
    }

    /// <summary>A message the engine refuses is reported as failed, with the engine's reason and without the message text.</summary>
    [Fact]
    public async Task OverlongMessage_IsReportedAsFailed_WithoutRepeatingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);
        var text = new string('x', 9000);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{text}\",\"mode\":\"send_now\"}}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var body = JsonDocument.Parse(raw);
        body.RootElement.GetProperty("delivery").GetString().ShouldBe("failed");
        body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        raw.ShouldNotContain(text, Case.Sensitive);
        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
    }

    /// <summary>Without live steering a send-now message is recorded and held, and a queued one is recorded as a follow-up.</summary>
    [Fact]
    public async Task WithoutLiveSteering_SendNowIsHeld_AndQueueIsQueued()
    {
        var ct = TestContext.Current.CancellationToken;
        var gated = new GatedHarnessFactory();
        using var factory = WebAppTestHost.Dev(more: services => services.AddSingleton<IHarnessSessionFactory>(gated));
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"run\"}", ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Running, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);

        using var held = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{SteerText}\",\"mode\":\"send_now\"}}", ct).ConfigureAwait(true);
        using var queued = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", "{\"text\":\"later\",\"mode\":\"queue\"}", ct).ConfigureAwait(true);

        using (var body = await WebAppTestHost.ReadJsonAsync(held, ct).ConfigureAwait(true))
        {
            held.StatusCode.ShouldBe(HttpStatusCode.OK);
            body.RootElement.GetProperty("delivery").GetString().ShouldBe("held");
            body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        }

        using (var body = await WebAppTestHost.ReadJsonAsync(queued, ct).ConfigureAwait(true))
        {
            body.RootElement.GetProperty("delivery").GetString().ShouldBe("queued");
        }

        var session = await factory.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        session.ShouldNotBeNull().Messages.Select(static message => message.Content).ShouldContain(SteerText, StringComparer.Ordinal);
        gated.Release();
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);
    }

    /// <summary>A session with no running turn answers 409 with the stable code; a closed session answers 409 session_closed.</summary>
    [Fact]
    public async Task NoRunningTurn_IsSteeringUnavailable_AndClosedIsSessionClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using (var idle = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", "{\"text\":\"hi\",\"mode\":\"send_now\"}", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(idle, HttpStatusCode.Conflict, "steering_unavailable", ct).ConfigureAwait(true);
        }

        using (var close = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/close", null, ct).ConfigureAwait(true))
        {
            close.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var closed = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", "{\"text\":\"hi\",\"mode\":\"queue\"}", ct).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(closed, HttpStatusCode.Conflict, "session_closed", ct).ConfigureAwait(true);
    }

    /// <summary>The body rules: 415 for another media type, 413 above the limit, 400 for a missing or invalid member or malformed JSON.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"text\":\"  \",\"mode\":\"queue\"}")]
    [InlineData("{\"text\":\"x\"}")]
    [InlineData("{\"mode\":\"queue\"}")]
    [InlineData("{\"text\":\"x\",\"mode\":\"later\"}")]
    [InlineData("not json")]
    [InlineData("")]
    public async Task Body_Invalid_IsBadRequest(string body)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var response = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", body, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A body that is not JSON by media type is 415 and one above the configured limit is 413.</summary>
    [Fact]
    public async Task Body_WrongMediaTypeOrTooLarge_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:MaxRequestBodyBytes"] = "64" });
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var text = new StringContent("{\"text\":\"x\",\"mode\":\"queue\"}", System.Text.Encoding.UTF8, "text/plain");
        using (var wrongType = await client.PostAsync(new Uri($"/sessions/{id}/steer", UriKind.Relative), text, ct).ConfigureAwait(true))
        {
            wrongType.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        }

        var big = new string('y', 200);
        using var large = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/steer", $"{{\"text\":\"{big}\",\"mode\":\"queue\"}}", ct).ConfigureAwait(true);
        large.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>Another owner's session and an absent session both answer 404, and nothing is recorded.</summary>
    [Fact]
    public async Task Steer_IsOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt(WebAppTestHost.HeldAtApproval());
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await WebAppTestHost.StartHeldRunAsync(alice, "hold", ct).ConfigureAwait(true);

        using var foreign = await WebAppTestHost.SendAsync(bob, HttpMethod.Post, $"/sessions/{id}/steer", "{\"text\":\"x\",\"mode\":\"send_now\"}", ct).ConfigureAwait(true);
        using var absent = await WebAppTestHost.SendAsync(bob, HttpMethod.Post, "/sessions/absent/steer", "{\"text\":\"x\",\"mode\":\"send_now\"}", ct).ConfigureAwait(true);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        factory.Services.GetRequiredService<LiveSessionTracker>().TryGetLive(id).ShouldBeOfType<InMemoryOmpSession>().SteeredMessages.ShouldBeEmpty();
        await WebAppTestHost.ReleaseHeldRunAsync(alice, id, ct).ConfigureAwait(true);
    }

    /// <summary>The pending list names the request a held run waits on, empties once it is decided, and is empty for a session with no run.</summary>
    [Fact]
    public async Task PendingToolApprovals_ListUndecidedRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var quiet = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);

        using (var response = await client.GetAsync(new Uri($"/sessions/{id}/tool-approvals", UriKind.Relative), ct).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("sessionId").GetString().ShouldBe(id);
            var approval = body.RootElement.GetProperty("approvals").EnumerateArray().ShouldHaveSingleItem();
            approval.GetProperty("toolName").GetString().ShouldBe(WebAppTestHost.HeldToolName);
            approval.GetProperty("requestId").GetString().ShouldNotBeNull().ShouldContain(":", Case.Sensitive);
            approval.GetProperty("grantableTools").GetArrayLength().ShouldBe(0);
        }

        using (var none = await client.GetAsync(new Uri($"/sessions/{quiet}/tool-approvals", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var body = await WebAppTestHost.ReadJsonAsync(none, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("approvals").GetArrayLength().ShouldBe(0);
        }

        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
        using var decided = await client.GetAsync(new Uri($"/sessions/{id}/tool-approvals", UriKind.Relative), ct).ConfigureAwait(true);
        using var after = await WebAppTestHost.ReadJsonAsync(decided, ct).ConfigureAwait(true);
        after.RootElement.GetProperty("approvals").GetArrayLength().ShouldBe(0);
    }

    /// <summary>Another owner reads 404 for the pending list.</summary>
    [Fact]
    public async Task PendingToolApprovals_AreOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using var foreign = await bob.GetAsync(new Uri($"/sessions/{id}/tool-approvals", UriKind.Relative), ct).ConfigureAwait(true);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
