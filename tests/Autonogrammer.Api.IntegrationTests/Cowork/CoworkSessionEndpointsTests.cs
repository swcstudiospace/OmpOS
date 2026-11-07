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

/// <summary>Owner, surface, cap, size and limiter rules of the Cowork summary routes.</summary>
public sealed class CoworkSessionEndpointsTests
{
    [Fact]
    public async Task Put_StoresCoworkSurface_OwnedByTheToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var id = Guid.NewGuid().ToString("N");

        using var response = await PutAsync(alice, id, "Ship the notes", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await host.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.OwnerId.ShouldBe(CoworkApiHost.Alice);
        stored.Surface.ShouldBe(SessionSurface.Cowork);
        stored.Messages.ShouldBeEmpty();
        stored.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_RejectsAnOwnerInTheBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var id = Guid.NewGuid().ToString("N");
        var json = Summary(id, "Notes");
        json = json[..^1] + ",\"ownerId\":\"bob\"}";

        using var response = await PutRawAsync(alice, id, json, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task Put_RejectsASurfaceInTheBody_AndNeverStoresIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var id = Guid.NewGuid().ToString("N");
        var json = Summary(id, "Notes");
        json = json[..^1] + ",\"surface\":\"code\"}";

        using var response = await PutRawAsync(alice, id, json, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ForeignRow_IsTheSame404AsAbsent_ForPutAndDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        using var bob = host.Client(CoworkApiHost.Bob);
        var id = Guid.NewGuid().ToString("N");
        using var created = await PutAsync(alice, id, "Alice", ct).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        var missing = Guid.NewGuid().ToString("N");

        using var foreignPut = await PutAsync(bob, id, "Stolen", ct).ConfigureAwait(true);
        using var absentDelete = await bob.DeleteAsync(Route(missing), ct).ConfigureAwait(true);
        using var foreignDelete = await bob.DeleteAsync(Route(id), ct).ConfigureAwait(true);

        foreignPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        absentDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var foreignBody = JsonDocument.Parse(await foreignPut.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        using var absentBody = JsonDocument.Parse(await absentDelete.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        foreignBody.RootElement.GetProperty("title").GetString().ShouldBe(absentBody.RootElement.GetProperty("title").GetString());
        foreignBody.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        var stored = await host.Services.GetRequiredService<ISessionRepository>().GetAsync(id, ct).ConfigureAwait(true);
        stored.ShouldNotBeNull();
        stored.OwnerId.ShouldBe(CoworkApiHost.Alice);
    }

    [Fact]
    public async Task Cap_201stDistinctId_Is409()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        for (var i = 0; i < 200; i++)
        {
            var id = $"cap{i:000}";
            using var stored = await PutAsync(alice, id, "Row", ct).ConfigureAwait(true);
            stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var rejected = await PutAsync(alice, "cap201", "Over", ct).ConfigureAwait(true);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Title_Over200Characters_IsCapped()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var id = Guid.NewGuid().ToString("N");

        using var response = await PutAsync(alice, id, new string('n', 250), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true));
        document.RootElement.GetProperty("title").GetString()!.Length.ShouldBe(CoworkSessionSummary.MaxTitleLength);
    }

    [Fact]
    public async Task OversizedBody_Is413_AndPlainText_Is415()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:MaxRequestBodyBytes"] = "32" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var id = Guid.NewGuid().ToString("N");
        using var huge = new StringContent(new string('x', 64), Encoding.UTF8, "application/json");
        using var tooBig = await alice.PutAsync(Route(id), huge, ct).ConfigureAwait(true);
        using var plain = new StringContent("not-json", Encoding.UTF8, "text/plain");
        using var wrongType = await alice.PutAsync(Route(id), plain, ct).ConfigureAwait(true);

        tooBig.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        wrongType.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Delete_RemovesTheOpenRow_AndRepeatedPut_Updates()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var repository = host.Services.GetRequiredService<ISessionRepository>();
        var id = Guid.NewGuid().ToString("N");
        using var created = await PutAsync(alice, id, "First", ct).ConfigureAwait(true);
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var updated = await PutAsync(alice, id, "Second", ct).ConfigureAwait(true);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var open = await repository.ListByOwnerAsync(CoworkApiHost.Alice, SessionSurface.Cowork, includeClosed: false, 10, null, ct).ConfigureAwait(true);
        open.Count.ShouldBe(1);

        using var deleted = await alice.DeleteAsync(Route(id), ct).ConfigureAwait(true);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await repository.ListByOwnerAsync(CoworkApiHost.Alice, SessionSurface.Cowork, includeClosed: false, 10, null, ct).ConfigureAwait(true);
        after.ShouldBeEmpty();
        using var again = await alice.DeleteAsync(Route(id), ct).ConfigureAwait(true);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Limiter_Answers429WithRetryAfter()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CoworkApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cowork:RequestsPerMinute"] = "1" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        var first = Guid.NewGuid().ToString("N");
        var second = Guid.NewGuid().ToString("N");
        using var allowed = await PutAsync(alice, first, "One", ct).ConfigureAwait(true);
        using var limited = await PutAsync(alice, second, "Two", ct).ConfigureAwait(true);

        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
        limited.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task RealPipeline_MapsBothRoutes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var id = Guid.NewGuid().ToString("N");

        using var put = await PutAsync(client, id, "Pipeline", ct).ConfigureAwait(true);
        using var delete = await client.DeleteAsync(Route(id), ct).ConfigureAwait(true);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static Uri Route(string id) => new($"/cowork/sessions/{id}", UriKind.Relative);

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string id, string title, CancellationToken cancellationToken)
    {
        using var body = new StringContent(Summary(id, title), Encoding.UTF8, "application/json");
        return await client.PutAsync(Route(id), body, cancellationToken).ConfigureAwait(true);
    }

    private static async Task<HttpResponseMessage> PutRawAsync(HttpClient client, string id, string json, CancellationToken cancellationToken)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(Route(id), body, cancellationToken).ConfigureAwait(true);
    }

    private static string Summary(string id, string title)
    {
        var summary = new CoworkSessionSummary
        {
            Id = id,
            Title = title,
            Status = CoworkSessionStatus.Created,
            CreatedAt = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse("2026-10-30T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        };
        return JsonSerializer.Serialize(summary, CoworkJsonContext.Default.CoworkSessionSummary);
    }
}
