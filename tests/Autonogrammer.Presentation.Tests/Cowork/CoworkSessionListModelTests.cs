using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cowork;

/// <summary>The Cowork rail groups by project, filters, selects one row and reports a full list.</summary>
public sealed class CoworkSessionListModelTests
{
    /// <summary>Rows are grouped by project, with tasks that have no project last.</summary>
    [Fact]
    public void RowsAreGroupedByProject()
    {
        var catalog = new ScriptedCatalog(
        [
            Row("a", "Alpha notes", "proj-a"),
            Row("b", "Beta notes", "proj-b"),
            Row("c", "Loose notes", null),
            Row("d", "Alpha follow-up", "proj-a"),
        ]);
        using var model = new CoworkSessionListModel(catalog);

        model.Groups.Select(group => group.ProjectId).ShouldBe(["proj-a", "proj-b", null]);
        model.Groups[0].Rows.Select(row => row.Id).ShouldBe(["a", "d"]);
        model.Groups[2].Heading.ShouldBe(CoworkCopy.NoProject);
    }

    /// <summary>The filter keeps matching titles and the selection tracks one row.</summary>
    [Fact]
    public void FilterNarrowsRowsAndSelectionTracksOneRow()
    {
        var catalog = new ScriptedCatalog(
        [
            Row("a", "Alpha notes", "proj-a"),
            Row("b", "Beta notes", "proj-b"),
        ]);
        using var model = new CoworkSessionListModel(catalog);

        model.Select("a");
        model.SelectedId.ShouldBe("a");
        model.Select("missing");
        model.SelectedId.ShouldBe("a");

        model.Filter = "beta";
        model.Rows.Select(row => row.Id).ShouldBe(["b"]);
        model.Selected?.Id.ShouldBe("a");
    }

    /// <summary>Two hundred rows show the exact full-list sentence.</summary>
    [Fact]
    public void TwoHundredRowsShowTheSessionLimitMessage()
    {
        var rows = Enumerable.Range(0, 200)
            .Select(index => Row("id-" + index, "Task " + index, "proj"))
            .ToArray();
        using var model = new CoworkSessionListModel(new ScriptedCatalog(rows));

        model.TotalCount.ShouldBe(200);
        model.LimitMessage.ShouldBe("You have 200 Cowork tasks. Delete or archive one to start another.");
    }

    private static CoworkSessionSummary Row(string id, string title, string? projectId) => new()
    {
        Id = id,
        Title = title,
        Status = CoworkSessionStatus.Created,
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        ProjectId = projectId,
    };

    private sealed class ScriptedCatalog(IReadOnlyList<CoworkSessionSummary> rows) : ICoworkSessionCatalog
    {
        public event EventHandler? Changed;

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => rows;

        public void Touch() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
