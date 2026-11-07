using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Contracts.Ui;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Ui;

/// <summary>
/// Verifies the additive artifact card: it round-trips, an old reader degrades kind artifact, and the current converter does not.
/// </summary>
public sealed class ArtifactBlockContractTests
{
    [Fact]
    public void ArtifactBlockRoundTripsThroughTheSourceContext()
    {
        var block = new ArtifactBlock
        {
            Id = "card-1",
            ArtifactId = "018f1b2c-3d4e-7a8b-9c0d-112233445566",
            Version = 2,
            Title = "Notes",
            ArtifactKind = ArtifactKind.Markdown,
            Action = ArtifactAction.Created,
        };

        var json = JsonSerializer.Serialize<UiBlock>(block, AutonogrammerJsonContext.Default.UiBlock);
        var back = JsonSerializer.Deserialize(json, AutonogrammerJsonContext.Default.UiBlock).ShouldBeOfType<ArtifactBlock>();

        back.Id.ShouldBe(block.Id);
        back.ArtifactId.ShouldBe(block.ArtifactId);
        back.Version.ShouldBe(block.Version);
        back.Title.ShouldBe(block.Title);
        back.ArtifactKind.ShouldBe(ArtifactKind.Markdown);
        back.Action.ShouldBe(ArtifactAction.Created);
        json.ShouldContain("\"kind\":\"artifact\"");
    }

    [Fact]
    public void AReaderWhoseMapLacksArtifactDegradesToUnknownBlock()
    {
        const string json = """{"kind":"artifact","id":"card-9","artifactId":"abc","version":1,"title":"Kept"}""";
        var bytes = Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes);
        _ = reader.Read();
        var back = new LegacyReader().Read(ref reader, typeof(UiBlock), new JsonSerializerOptions()).ShouldBeOfType<UnknownBlock>();

        back.OriginalKind.ShouldBe("artifact");
        back.Id.ShouldBe("card-9");
        back.Raw.GetProperty("artifactId").GetString().ShouldBe("abc");
    }

    [Fact]
    public void TheUpdatedConverterMapsArtifactToArtifactBlock()
    {
        const string json = """{"kind":"artifact","id":"card-3","artifactId":"abc","version":4,"title":"Panel","artifactKind":"Svg","action":"Updated"}""";

        var back = JsonSerializer.Deserialize(json, AutonogrammerJsonContext.Default.UiBlock).ShouldBeOfType<ArtifactBlock>();

        back.ArtifactId.ShouldBe("abc");
        back.Version.ShouldBe(4);
        back.ArtifactKind.ShouldBe(ArtifactKind.Svg);
        back.Action.ShouldBe(ArtifactAction.Updated);
    }

    /// <summary>A reader whose kind map has no artifact entry. Unknown kinds keep the original kind and the raw id.</summary>
    private sealed class LegacyReader : JsonConverter<UiBlock>
    {
        public override UiBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var kind = root.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() : null;
            var id = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString() ?? string.Empty
                : string.Empty;
            return new UnknownBlock { Id = id, OriginalKind = kind, Raw = root.Clone() };
        }

        public override void Write(Utf8JsonWriter writer, UiBlock value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
