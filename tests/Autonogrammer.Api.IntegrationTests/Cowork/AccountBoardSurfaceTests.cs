using System.Net;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cowork;

/// <summary>A Cowork row stays off the Code board and the Code session list, in memory and on PostgreSQL.</summary>
public sealed class AccountBoardSurfaceTests
{
    [Fact]
    public async Task InMemory_CoworkRow_LeavesTheBoardAndSessionListEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Accounts:Enabled"] = "true" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var cowork = Guid.NewGuid().ToString("N");
        using var put = await PutAsync(alice, cowork, ct).ConfigureAwait(true);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var board = await alice.GetAsync(new Uri("/account/board", UriKind.Relative), ct).ConfigureAwait(true);
        using var sessions = await alice.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var account = await alice.GetAsync(new Uri("/account/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        var boardText = await board.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var sessionsText = await sessions.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var accountText = await account.Content.ReadAsStringAsync(ct).ConfigureAwait(true);

        board.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessions.StatusCode.ShouldBe(HttpStatusCode.OK);
        account.StatusCode.ShouldBe(HttpStatusCode.OK);
        boardText.ShouldNotContain(cowork);
        sessionsText.ShouldNotContain(cowork);
        accountText.ShouldContain(cowork);
        accountText.ShouldContain("cowork");
    }

    [Fact]
    public async Task Postgres_CoworkRow_LeavesTheBoardAndSessionListEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory(PostgresSessionConfig());
        using var client = factory.CreateClient();
        var code = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        await factory.Services.GetRequiredService<ISessionRepository>().SaveAsync(
            new Session
            {
                Id = code,
                OwnerId = "local-dev",
                CreatedAt = now,
                UpdatedAt = now,
                Status = SessionStatus.Idle,
                Surface = SessionSurface.Code,
                Messages = [],
                Events = [],
            },
            ct).ConfigureAwait(true);
        var cowork = Guid.NewGuid().ToString("N");
        using var put = await PutAsync(client, cowork, ct).ConfigureAwait(true);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var board = await client.GetAsync(new Uri("/account/board", UriKind.Relative), ct).ConfigureAwait(true);
        using var sessions = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var account = await client.GetAsync(new Uri("/account/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        var boardText = await board.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var sessionsText = await sessions.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var accountText = await account.Content.ReadAsStringAsync(ct).ConfigureAwait(true);

        board.StatusCode.ShouldBe(HttpStatusCode.OK);
        boardText.ShouldContain(code);
        boardText.ShouldNotContain(cowork);
        sessionsText.ShouldContain(code);
        sessionsText.ShouldNotContain(cowork);
        accountText.ShouldContain(code);
        accountText.ShouldContain(cowork);
        accountText.ShouldContain("cowork");
        accountText.ShouldContain("code");
    }

    private static Dictionary<string, string?> PostgresSessionConfig() =>
        new(StringComparer.Ordinal)
        {
            ["Accounts:Enabled"] = "true",
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = JwtTestHost.PostgresConnectionString(),
            ["Harness:Adapter"] = "InMemory",
        };

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string id, CancellationToken cancellationToken)
    {
        var summary = new CoworkSessionSummary
        {
            Id = id,
            Title = "Cowork notes",
            Status = CoworkSessionStatus.Created,
            CreatedAt = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };
        using var body = new StringContent(JsonSerializer.Serialize(summary, CoworkJsonContext.Default.CoworkSessionSummary), Encoding.UTF8, "application/json");
        return await client.PutAsync(new Uri($"/cowork/sessions/{id}", UriKind.Relative), body, cancellationToken).ConfigureAwait(true);
    }
}
