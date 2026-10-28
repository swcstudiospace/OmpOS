using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>
/// The card state keeps status and the stale flag independent (VX-04): a stale change leaves the status alone, and a runtime signal
/// leaves the stale flag alone.
/// </summary>
public sealed class KanbanCardStateTests
{
    /// <summary>Setting the stale flag changes only the flag, in both directions.</summary>
    [Fact]
    public void StaleFlagChangesWithoutChangingTheStatus()
    {
        var card = new KanbanCardState(RuntimeStatus.Running, IsStale: false);

        var stale = card.WithStale(true);
        stale.Status.ShouldBe(RuntimeStatus.Running);
        stale.IsStale.ShouldBeTrue();

        var fresh = stale.WithStale(false);
        fresh.Status.ShouldBe(RuntimeStatus.Running);
        fresh.IsStale.ShouldBeFalse();
    }

    /// <summary>A runtime signal changes only the status, whether the card is stale or fresh.</summary>
    [Fact]
    public void StatusChangesWithoutChangingTheStaleFlag()
    {
        var stale = new KanbanCardState(RuntimeStatus.Running, IsStale: true);
        var failed = stale.ApplySignal(new TestRuntimeSignal(RuntimeSignalKind.RunFailed));
        failed.Status.ShouldBe(RuntimeStatus.Failed);
        failed.IsStale.ShouldBeTrue();

        var fresh = new KanbanCardState(RuntimeStatus.Running, IsStale: false);
        var completed = fresh.ApplySignal(new TestRuntimeSignal(RuntimeSignalKind.RunCompleted));
        completed.Status.ShouldBe(RuntimeStatus.Completed);
        completed.IsStale.ShouldBeFalse();
    }

    /// <summary>A signal kind with no mapping is refused, and the card keeps its status and stale flag.</summary>
    [Fact]
    public void AnUnmappedSignalIsRefusedAndTheCardIsUnchanged()
    {
        var card = new KanbanCardState(RuntimeStatus.Running, IsStale: true);

        Should.Throw<UnmappedRuntimeSignalException>(() => card.ApplySignal(new TestRuntimeSignal((RuntimeSignalKind)999)));

        card.ShouldBe(new KanbanCardState(RuntimeStatus.Running, IsStale: true));
    }

    /// <summary>A refused signal names the signal parameter, not the kind value, and carries the refused kind.</summary>
    [Fact]
    public void AnUnmappedSignalNamesTheSignalParameter()
    {
        var card = new KanbanCardState(RuntimeStatus.Running, IsStale: false);

        var error = Should.Throw<UnmappedRuntimeSignalException>(() => card.ApplySignal(new TestRuntimeSignal((RuntimeSignalKind)999)));

        error.ParamName.ShouldBe("signal");
        error.Kind.ShouldBe((RuntimeSignalKind?)(RuntimeSignalKind)999);
    }
}
