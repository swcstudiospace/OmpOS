using System.Net;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Accounts;

/// <summary><c>GET /board</c> is gone. The board and the session list come from the repository.</summary>
public sealed class BoardCutoverTests
{
    /// <summary>The old board route is absent and the account board serves the same Code rows.</summary>
    [Fact]
    public async Task BoardRoute_IsGone_AccountBoardServesCodeRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using var gone = await alice.GetAsync(new Uri("/board", UriKind.Relative), ct).ConfigureAwait(true);
        gone.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);

        using var board = await alice.GetAsync(new Uri("/account/board", UriKind.Relative), ct).ConfigureAwait(true);
        var text = await board.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        board.StatusCode.ShouldBe(HttpStatusCode.OK);
        text.ShouldContain(id);
    }

    /// <summary>A session saved only in the repository is listed, so the list does not depend on a process-local index.</summary>
    [Fact]
    public async Task RepositorySession_IsListedWithoutAProcessIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        var id = "stored-" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        await factory.Services.GetRequiredService<ISessionRepository>().SaveAsync(
            new Session
            {
                Id = id,
                OwnerId = "alice",
                CreatedAt = now,
                UpdatedAt = now,
                Status = SessionStatus.Idle,
                Surface = SessionSurface.Code,
                Messages = [],
                Events = [],
            },
            ct).ConfigureAwait(true);

        using var list = await alice.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        (await list.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldContain(id);
    }
}
