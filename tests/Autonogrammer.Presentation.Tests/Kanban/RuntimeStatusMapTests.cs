using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>
/// The signal-to-status table (Phase 36 criterion 3): every signal kind maps to its recorded status, every status is the target of a
/// kind, the table covers every kind, and an undefined kind is refused rather than given a default status.
/// </summary>
public sealed class RuntimeStatusMapTests
{
    /// <summary>The recorded table, one row per signal kind. A kind added to the enum without a row here fails the completeness test.</summary>
    private static readonly Dictionary<RuntimeSignalKind, RuntimeStatus> Table = new()
    {
        [RuntimeSignalKind.RunQueued] = RuntimeStatus.Queued,
        [RuntimeSignalKind.RunStarted] = RuntimeStatus.Running,
        [RuntimeSignalKind.InputRequested] = RuntimeStatus.AwaitingInput,
        [RuntimeSignalKind.ApprovalRequested] = RuntimeStatus.NeedsAttention,
        [RuntimeSignalKind.RunPaused] = RuntimeStatus.Paused,
        [RuntimeSignalKind.RunCompleted] = RuntimeStatus.Completed,
        [RuntimeSignalKind.RunFailed] = RuntimeStatus.Failed,
        [RuntimeSignalKind.SessionUnreachable] = RuntimeStatus.Unreachable,
    };

    /// <summary>Each signal kind maps to the status recorded for it.</summary>
    [Fact]
    public void EverySignalKindMapsToItsRecordedStatus()
    {
        foreach (var row in Table)
        {
            RuntimeStatusMap.StatusOf(row.Key).ShouldBe(row.Value, $"signal kind {row.Key}");
        }
    }

    /// <summary>The recorded table names every defined kind, so none is left without a mapping.</summary>
    [Fact]
    public void EveryDefinedSignalKindHasARecordedRow()
    {
        var kinds = Enum.GetValues<RuntimeSignalKind>();

        Table.Count.ShouldBe(kinds.Length);
        kinds.ShouldAllBe(kind => Table.ContainsKey(kind));
    }

    /// <summary>Every runtime status is reached from at least one signal kind.</summary>
    [Fact]
    public void EveryRuntimeStatusIsTheTargetOfASignalKind()
    {
        Enum.GetValues<RuntimeSignalKind>()
            .Select(RuntimeStatusMap.StatusOf)
            .Distinct()
            .Order()
            .ToArray()
            .ShouldBe(Enum.GetValues<RuntimeStatus>());
    }

    /// <summary>A kind outside the enum is refused with an explicit error that names the kind; no status is returned.</summary>
    [Fact]
    public void AnUndefinedSignalKindIsRefusedWithAnExplicitError()
    {
        const RuntimeSignalKind undefined = (RuntimeSignalKind)999;

        var error = Should.Throw<UnmappedRuntimeSignalException>(() => RuntimeStatusMap.StatusOf(undefined));

        error.Kind.ShouldBe((RuntimeSignalKind?)undefined);
    }
}
