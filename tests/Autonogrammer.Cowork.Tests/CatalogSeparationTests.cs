using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests;

/// <summary>Cowork list types stay off the Code catalog.</summary>
public sealed class CatalogSeparationTests
{
    [Fact]
    public void NoLoadedTypeImplementsBothCatalogs()
    {
        var assemblies = new[]
        {
            typeof(CoworkSessionSummary).Assembly,
            typeof(AssemblyMarker).Assembly,
        };

        var both = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Where(type => typeof(ICoworkSessionCatalog).IsAssignableFrom(type) && typeof(ISessionCatalog).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        both.ShouldBeEmpty();
    }

    [Fact]
    public void NoCoworkRowIsASessionSummary()
    {
        var rows = typeof(CoworkSessionSummary).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, typeof(CoworkSessionSummary).Namespace, StringComparison.Ordinal))
            .Where(type => typeof(SessionSummary).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        rows.ShouldBeEmpty();
    }

    [Fact]
    public void StatusHasExactlyTheCoworkValues()
    {
        Enum.GetNames<CoworkSessionStatus>().ShouldBe(
            ["Created", "Running", "WaitingForDecision", "Paused", "Completed", "Failed", "Lost"]);
    }

    [Fact]
    public void SummaryRoundTripsStatusAsAStringAndRefusesAnUnknownString()
    {
        var summary = new CoworkSessionSummary
        {
            Id = "s-1",
            Title = new string('a', 220),
            Status = CoworkSessionStatus.WaitingForDecision,
            CreatedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 10, 30, 12, 5, 0, TimeSpan.Zero),
            ProjectId = "p-1",
            DeviceId = "d-1",
        };

        var json = JsonSerializer.Serialize(summary, CoworkJsonContext.Default.CoworkSessionSummary);
        json.ShouldContain("\"status\":\"WaitingForDecision\"");
        json.ShouldNotContain("\"status\":1");
        json.ShouldNotContain("messages");
        json.ShouldNotContain("trustedFolders");

        var restored = JsonSerializer.Deserialize(json, CoworkJsonContext.Default.CoworkSessionSummary);
        restored.ShouldNotBeNull();
        restored.Title.Length.ShouldBe(CoworkSessionSummary.MaxTitleLength);
        restored.Status.ShouldBe(CoworkSessionStatus.WaitingForDecision);
        restored.ProjectId.ShouldBe("p-1");
        restored.DeviceId.ShouldBe("d-1");

        var unknown = json.Replace("WaitingForDecision", "NotAStatus", StringComparison.Ordinal);
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(unknown, CoworkJsonContext.Default.CoworkSessionSummary));
    }
}
