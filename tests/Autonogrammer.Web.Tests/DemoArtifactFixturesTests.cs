using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Autonogrammer.Web.Demo;
using Autonogrammer.Web.Services.Artifacts;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The Demo's artifact bodies are in the shape of the API's documents: each one serializes and reads back to the same JSON through the
/// web's source-generated context, and each header's hash is the hash of the bytes the Demo stores for it.
/// </summary>
public sealed class DemoArtifactFixturesTests
{
    /// <summary>Every Demo artifact body round-trips through the context to the same JSON.</summary>
    [Fact]
    public void EveryDemoArtifactBodyRoundTripsThroughTheContext()
    {
        var list = DemoArtifactFixtures.List();
        RoundTrip(list, ArtifactWebJsonContext.Default.ArtifactListRecord);
        foreach (var header in list.Artifacts)
        {
            RoundTrip(header, ArtifactWebJsonContext.Default.ArtifactSummaryRecord);
        }

        RoundTrip(new RenderTicketRecord("/cowork/artifact-render/example"), ArtifactWebJsonContext.Default.RenderTicketRecord);
    }

    /// <summary>Each header's hash is the SHA-256 of the bytes the Demo stores; the metadata-only artifact hashes its identifier.</summary>
    [Fact]
    public void EachHeaderHashIsTheHashOfItsStoredBytes()
    {
        foreach (var header in DemoArtifactFixtures.List().Artifacts)
        {
            var stored = DemoArtifactFixtures.Content(header.Id, header.Version) ?? System.Text.Encoding.UTF8.GetBytes(header.Id);

            header.ContentSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(stored)));
        }
    }

    /// <summary>The metadata-only artifact is listed but has no stored content, so the Demo answers it as sync is off.</summary>
    [Fact]
    public void TheMetadataOnlyArtifactHasNoStoredContent()
    {
        DemoArtifactFixtures.Find(DemoArtifactFixtures.MetadataOnlyId).ShouldNotBeNull();
        DemoArtifactFixtures.Content(DemoArtifactFixtures.MetadataOnlyId, 1).ShouldBeNull();
    }

    /// <summary>Only version 1 exists in the Demo.</summary>
    [Fact]
    public void OnlyVersionOneHasContent()
    {
        DemoArtifactFixtures.Content(DemoArtifactFixtures.NotesId, 2).ShouldBeNull();
    }

    private static void RoundTrip<T>(T value, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        var back = JsonSerializer.Deserialize(json, typeInfo).ShouldNotBeNull();
        JsonSerializer.Serialize(back, typeInfo).ShouldBe(json);
    }
}
