using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Serialization;
using OmpOS.Web.Demo;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>
/// Every body the Demo answers deserializes through the Contracts source-generated contexts and serializes back to the same JSON, so the Demo can neither
/// invent a member (it would be lost on the way back) nor drop one of the real contract. Strict unmapped-member handling is not usable here: the payload
/// converter reads the <c>kind</c> discriminator and deserializes the rest with the same options.
/// </summary>
public sealed class DemoFixturesContractTests : IDisposable
{

    private readonly DemoTestHost _host = new();

    /// <summary>The catalogue, the workspace, the source and the board are strict contract shapes.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task WorkspaceLevelBodiesAreStrictContractShapes()
    {
        await RoundTripAsync("sessions", CatalogJsonContext.Default.SessionCatalogEntryArray).ConfigureAwait(true);
        await RoundTripAsync("workspace", CatalogJsonContext.Default.WorkspaceInfo).ConfigureAwait(true);
        await RoundTripAsync("source", CatalogJsonContext.Default.SourceInfo).ConfigureAwait(true);
        await RoundTripAsync("board", CatalogJsonContext.Default.BoardResponse).ConfigureAwait(true);
    }

    /// <summary>Every session's stored form and every per-session body are strict contract shapes.</summary>
    /// <param name="id">The session.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData(DemoFixtures.RunningId)]
    [InlineData(DemoFixtures.ApprovalId)]
    [InlineData(DemoFixtures.TerminalId)]
    [InlineData(DemoFixtures.CompletedId)]
    [InlineData(DemoFixtures.FailedId)]
    [InlineData(DemoFixtures.ClosedId)]
    public async Task PerSessionBodiesAreStrictContractShapes(string id)
    {
        await RoundTripAsync("sessions/" + id, OmpOsJsonContext.Default.Session).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/stages", CatalogJsonContext.Default.SessionStagesResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/context", CatalogJsonContext.Default.SessionContextResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/card-details", CatalogJsonContext.Default.CardDetailsResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/config-options", SessionControlsJsonContext.Default.SessionConfigOptionsResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/commands", SessionControlsJsonContext.Default.SessionCommandsResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/changes", SessionControlsJsonContext.Default.SessionChangesResponse).ConfigureAwait(true);
        await RoundTripAsync("sessions/" + id + "/tool-approvals", SessionControlsJsonContext.Default.PendingToolApprovalsResponse).ConfigureAwait(true);
    }

    /// <summary>A problem body has the members of an RFC 9457 problem and the code in the type, as the client reads it.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ProblemBodiesCarryTypeTitleStatusAndDetail()
    {
        var ct = TestContext.Current.CancellationToken;
        using var conflict = await _host.Http.PostAsync(new Uri("sessions/" + DemoFixtures.CompletedId + "/reopen", UriKind.Relative), null, ct).ConfigureAwait(true);

        var problem = JsonNode.Parse(await conflict.Content.ReadAsStringAsync(ct).ConfigureAwait(true))!;

        conflict.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        problem["type"]!.GetValue<string>().ShouldBe("/problems/session_not_closed");
        problem["status"]!.GetValue<int>().ShouldBe(409);
        problem["title"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        problem["detail"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private async Task RoundTripAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var ct = TestContext.Current.CancellationToken;
        using var response = await _host.Http.GetAsync(new Uri(path, UriKind.Relative), ct).ConfigureAwait(true);
        response.IsSuccessStatusCode.ShouldBeTrue(path + " must answer 2xx");
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);

        var value = JsonSerializer.Deserialize(text, typeInfo);

        value.ShouldNotBeNull(path);
        var again = JsonSerializer.Serialize(value, typeInfo);
        JsonNode.DeepEquals(JsonNode.Parse(text), JsonNode.Parse(again)).ShouldBeTrue(path + " must serialize back to the same JSON");
    }
}
