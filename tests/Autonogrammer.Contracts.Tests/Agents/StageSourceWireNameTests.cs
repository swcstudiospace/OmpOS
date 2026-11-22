using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Agents;

/// <summary>Verifies the plugin-only frontend boundary at the backend presentation contract: the engine stage source crosses the wire under the plugin-neutral name <c>Engine</c>, never the engine name, and reads back to the same member.</summary>
public sealed class StageSourceWireNameTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The engine source serializes as <c>Engine</c> and contains no engine name, while the C# member stays <see cref="StageSource.Omp"/>.</summary>
    [Fact]
    public void EngineSource_SerializesUnderTheNeutralName()
    {
        StageSource.Omp.ToString().ShouldBe("Omp");

        var response = new SessionStagesResponse
        {
            SessionId = "s1",
            Available = true,
            Stages = [new StageEvent { SessionId = "s1", Source = StageSource.Omp, StageId = "turn", Title = "Engine turn", Status = StageStatus.Running, Timestamp = Stamp }],
        };

        var json = JsonSerializer.Serialize(response, CatalogJsonContext.Default.SessionStagesResponse);
        json.ShouldContain("\"Engine\"", Case.Sensitive);
        json.ShouldNotContain("Omp", Case.Insensitive);

        var readBack = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.SessionStagesResponse);
        readBack.ShouldNotBeNull().Stages.ShouldHaveSingleItem().Source.ShouldBe(StageSource.Omp);
    }
}
