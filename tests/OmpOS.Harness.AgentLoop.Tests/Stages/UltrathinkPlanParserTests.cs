using OmpOS.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Stages;

/// <summary>Feeds trimmed real Ultrathink session records, and broken ones, to the pure parser.</summary>
public sealed class UltrathinkPlanParserTests
{
    [Fact]
    public void PartialTrackingRecordSummarisesNodesWavesAndClarifications()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Bytes("ultrathink-session-partial.json"));

        summary.ShouldNotBeNull();
        summary.IsFallback.ShouldBeFalse();
        summary.GraphId.ShouldBe("ut-mvp0bh59-2dd6ef4c");
        summary.NodeCount.ShouldBe(8);
        summary.WaveCount.ShouldBe(4);
        summary.ClarificationCount.ShouldBe(3);
        summary.TrackingStatus.ShouldBe("partial");
    }

    [Fact]
    public void CompleteRecordCountsTheDeepestDependencyChainAsWaves()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Bytes("ultrathink-session-complete.json"));

        summary.ShouldNotBeNull();
        summary.NodeCount.ShouldBe(8);
        summary.WaveCount.ShouldBe(6);
        summary.ClarificationCount.ShouldBe(6);
        summary.TrackingStatus.ShouldBe("complete");
    }

    [Fact]
    public void FallbackRecordIsMarkedFallbackAndHasNoGraphId()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Bytes("ultrathink-session-fallback.json"));

        summary.ShouldNotBeNull();
        summary.IsFallback.ShouldBeTrue();
        summary.GraphId.ShouldBeNull();
        summary.TrackingStatus.ShouldBeNull();
    }

    [Fact]
    public void RecordWithoutAPlanIsAFallbackEvenWhenTheSourceSaysLlm()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8("""{"result":{"source":"llm"},"graph":{"nodes":[{"id":"n1","dependsOn":[]}]}}"""));

        summary.ShouldNotBeNull();
        summary.IsFallback.ShouldBeTrue();
        summary.NodeCount.ShouldBe(1);
    }

    [Fact]
    public void RecordWithAPlanButNoNodesIsAFallback()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8("""{"result":{"source":"llm"},"plan":{"graphId":"ut-abc-0123abcd"}}"""));

        summary.ShouldNotBeNull();
        summary.IsFallback.ShouldBeTrue();
        summary.NodeCount.ShouldBe(0);
        summary.WaveCount.ShouldBe(0);
    }

    [Fact]
    public void GraphEmbeddedInTheResultIsUsedWhenTheTopLevelGraphIsAbsent()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8(
            """{"result":{"source":"llm","graph":{"nodes":[{"id":"a"},{"id":"b","dependsOn":["a"]}]}},"plan":{"graphId":"ut-abc-0123abcd"}}"""));

        summary.ShouldNotBeNull();
        summary.NodeCount.ShouldBe(2);
        summary.WaveCount.ShouldBe(2);
    }

    [Theory]
    [InlineData("not-a-graph-id")]
    [InlineData("ut-ABC-0123abcd")]
    [InlineData("ut-abc-0123abc")]
    [InlineData("ut-abc-0123abcd\nextra")]
    public void GraphIdThatIsNotAGraphIdentifierIsDropped(string graphId)
    {
        var json = "{\"result\":{\"source\":\"llm\"},\"graph\":{\"nodes\":[{\"id\":\"n1\"}]},\"plan\":{\"graphId\":\"" + graphId.Replace("\n", "\\n", StringComparison.Ordinal) + "\"}}";

        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8(json));

        summary.ShouldNotBeNull();
        summary.GraphId.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("{\"result\":")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{\"graph\":{\"nodes\":\"not a list\"}}")]
    [InlineData("{\"clarifications\":5}")]
    public void ContentThatIsNotASessionRecordYieldsNullAndNeverThrows(string content)
    {
        UltrathinkPlanParser.Parse(StageFixtures.Utf8(content)).ShouldBeNull();
    }

    [Fact]
    public void UnknownMembersAtEveryLevelAreSkipped()
    {
        var json = """
            {"newVendorField":{"a":[1,2,3]},
             "result":{"source":"llm","somethingNew":true},
             "graph":{"goal":"g","extra":null,"nodes":[{"id":"n1","kind":"understand","futureKey":1,"dependsOn":[]}]},
             "plan":{"graphId":"ut-abc-0123abcd","issues":[{"x":1}]},
             "tracking":{"status":"complete","errors":["ignored"]}}
            """;

        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8(json));

        summary.ShouldNotBeNull();
        summary.IsFallback.ShouldBeFalse();
        summary.NodeCount.ShouldBe(1);
        summary.TrackingStatus.ShouldBe("complete");
    }

    [Theory]
    [InlineData("PARTIAL", "partial")]
    [InlineData(" Failed ", "failed")]
    [InlineData("complete", "complete")]
    [InlineData("in-flight", null)]
    [InlineData("https://example.test/tracking?token=abc", null)]
    public void TrackingStatusIsReducedToTheKnownWords(string raw, string? expected)
    {
        var json = "{\"result\":{\"source\":\"llm\"},\"graph\":{\"nodes\":[{\"id\":\"n1\"}]},\"plan\":{},\"tracking\":{\"status\":\"" + raw + "\"}}";

        UltrathinkPlanParser.Parse(StageFixtures.Utf8(json))!.TrackingStatus.ShouldBe(expected);
    }

    [Fact]
    public void ACycleAndUnknownDependenciesDoNotHangAndAreBounded()
    {
        var json = """{"result":{"source":"llm"},"plan":{},"graph":{"nodes":[{"id":"a","dependsOn":["b"]},{"id":"b","dependsOn":["a"]},{"id":"c","dependsOn":["missing","c"]}]}}""";

        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8(json));

        summary.ShouldNotBeNull();
        summary.NodeCount.ShouldBe(3);
        summary.WaveCount.ShouldBeInRange(1, 3);
    }

    [Fact]
    public void NullNodeEntriesAreNotCounted()
    {
        var summary = UltrathinkPlanParser.Parse(StageFixtures.Utf8("""{"result":{"source":"llm"},"plan":{},"graph":{"nodes":[null,{"id":"a"},null]}}"""));

        summary.ShouldNotBeNull();
        summary.NodeCount.ShouldBe(1);
    }
}
