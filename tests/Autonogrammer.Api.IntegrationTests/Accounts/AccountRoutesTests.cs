using System.Net;
using Autonogrammer.Api.IntegrationTests.Security;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Accounts;

/// <summary>The four owner-scoped account routes.</summary>
public sealed class AccountRoutesTests
{
    /// <summary>Each owner sees only their own sessions, and a foreign item is the same 404 as an absent one.</summary>
    [Fact]
    public async Task CrossOwner_IsTheSame404AsAbsent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using var aliceList = await alice.GetAsync(new Uri("/account/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var bobList = await bob.GetAsync(new Uri("/account/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var aliceItem = await alice.GetAsync(new Uri($"/account/sessions/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        using var foreign = await bob.GetAsync(new Uri($"/account/sessions/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        using var absent = await alice.GetAsync(new Uri("/account/sessions/missing", UriKind.Relative), ct).ConfigureAwait(true);

        aliceList.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await WebAppTestHost.ReadJsonAsync(aliceList, ct).ConfigureAwait(true)).RootElement.GetProperty("items").GetArrayLength().ShouldBe(1);
        (await WebAppTestHost.ReadJsonAsync(bobList, ct).ConfigureAwait(true)).RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
        aliceItem.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignBody = await WebAppTestHost.ReadJsonAsync(foreign, ct).ConfigureAwait(true);
        using var absentBody = await WebAppTestHost.ReadJsonAsync(absent, ct).ConfigureAwait(true);
        foreignBody.RootElement.GetProperty("title").GetString().ShouldBe(absentBody.RootElement.GetProperty("title").GetString());
        foreignBody.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
    }

    /// <summary>A Cowork session is listed on the account and nowhere on the Code catalog, the board or the event stream.</summary>
    [Fact]
    public async Task CoworkSession_AppearsOnlyInAccountSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        var code = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);
        var cowork = "cowork-" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await factory.Services.GetRequiredService<ISessionRepository>().SaveAsync(
            new Session
            {
                Id = cowork,
                OwnerId = "alice",
                CreatedAt = now,
                UpdatedAt = now,
                Status = SessionStatus.Idle,
                Surface = SessionSurface.Cowork,
                Messages = [],
                Events = [],
            },
            ct).ConfigureAwait(true);

        using var account = await alice.GetAsync(new Uri("/account/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var sessions = await alice.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var board = await alice.GetAsync(new Uri("/account/board", UriKind.Relative), ct).ConfigureAwait(true);
        var accountText = await account.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var sessionsText = await sessions.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var boardText = await board.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        accountText.ShouldContain(cowork);
        accountText.ShouldContain("cowork");
        accountText.ShouldContain(code);
        sessionsText.ShouldNotContain(cowork);
        boardText.ShouldNotContain(cowork);

        await factory.Services.GetRequiredService<Autonogrammer.Api.Services.CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);
        var stream = await SessionClient.ReadStreamAsync(alice, "/events", 1, ct, lastEventId: "0").ConfigureAwait(true);
        string.Join('\n', stream.Select(static item => item.Data)).ShouldNotContain(cowork);
    }

    /// <summary>Features list cowork and not dispatch. Owner-token mode answers without a row. Account routes need a token.</summary>
    [Fact]
    public async Task Features_OwnerToken_AndAnonymous()
    {
        var ct = TestContext.Current.CancellationToken;
        using var oidc = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(oidc, "alice");
        using var account = await alice.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(true);
        using var body = await WebAppTestHost.ReadJsonAsync(account, ct).ConfigureAwait(true);
        var features = body.RootElement.GetProperty("features").EnumerateArray().Select(static item => item.GetString()).ToArray();
        features.ShouldBe(["cowork"]);
        body.RootElement.TryGetProperty("email", out _).ShouldBeFalse();

        using var owner = OwnerTokenTestHost.CreateFactory();
        using var ownerClient = owner.CreateClient();
        OwnerTokenTestHost.UseBearer(ownerClient, OwnerTokenTestHost.Token);
        using var ownerAccount = await ownerClient.GetAsync(new Uri("/account", UriKind.Relative), ct).ConfigureAwait(true);
        using var ownerBody = await WebAppTestHost.ReadJsonAsync(ownerAccount, ct).ConfigureAwait(true);
        ownerBody.RootElement.GetProperty("id").GetString().ShouldBe("operator");
        ownerBody.RootElement.TryGetProperty("email", out _).ShouldBeFalse();
        ownerBody.RootElement.TryGetProperty("createdAt", out _).ShouldBeFalse();
        ownerBody.RootElement.TryGetProperty("lastSeenAt", out _).ShouldBeFalse();
        (await owner.Services.GetRequiredService<Autonogrammer.Api.Accounts.IAccountStore>().CountAsync(ct).ConfigureAwait(true)).ShouldBe(0);

        using var anonymous = oidc.CreateClient();
        foreach (var path in new[] { "/account", "/account/sessions", "/account/sessions/x", "/account/board" })
        {
            using var denied = await anonymous.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(true);
            denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
