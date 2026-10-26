using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>Drag and drop on the Kanban tab: only Backlog and Archived accept a dropped card; a runtime column refuses it and nothing changes.</summary>
public sealed class KanbanBoardDropTests
{
    private static readonly string[] ColumnKeys = ["backlog", "queued", "running", "awaiting-input", "needs-attention", "paused", "completed", "failed", "unreachable", "archived"];

    /// <summary>Exactly the Backlog and Archived columns allow a drop.</summary>
    [Fact]
    public async Task OnlyBacklogAndArchivedAllowADrop()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();

            ColumnKeys.Where(key => DragDrop.GetAllowDrop(fixture.View.ColumnOf(key)!)).ShouldBe(["backlog", "archived"]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A drop on a runtime column is refused and leaves the card where it was; a drop on Archived or Backlog moves the same card control there.</summary>
    [Fact]
    public async Task DropsMoveCardsOnlyToUserColumns()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open();
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();

            fixture.View.HandleDrop("completed", "run-1").ShouldBe(KanbanMoveOutcome.RefusedRuntimeColumn);
            fixture.Settle();
            card.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("running")!);
            fixture.Board.CardOf("run-1")!.Status.ShouldBe(RuntimeStatus.Running);

            fixture.View.HandleDrop("archived", "run-1").ShouldBe(KanbanMoveOutcome.Moved);
            fixture.Settle();
            fixture.View.CardViewOf("run-1").ShouldBeSameAs(card);
            card.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("archived")!);
            fixture.View.ColumnHeaderOf("archived")!.Text.ShouldBe("Archived  1");
            fixture.View.ColumnHeaderOf("running")!.Text.ShouldBe("Running  1");
            fixture.Board.CardOf("run-1")!.Status.ShouldBe(RuntimeStatus.Running, "placing a card never sets a runtime status");

            fixture.View.HandleDrop("backlog", "run-1").ShouldBe(KanbanMoveOutcome.Moved);
            fixture.Settle();
            card.GetVisualAncestors().ShouldContain(fixture.View.ColumnOf("backlog")!);

            fixture.View.HandleDrop("backlog", null).ShouldBe(KanbanMoveOutcome.UnknownCard);
            fixture.View.HandleDrop("backlog", "nobody").ShouldBe(KanbanMoveOutcome.UnknownCard);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
