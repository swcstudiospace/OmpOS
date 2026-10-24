using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Agent.Tests;

/// <summary>The contract's version, the kind of every harness event type, and the envelope's refusals.</summary>
public sealed class OmpAgentContractTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 21, 17, 13, 51, TimeSpan.Zero);

    /// <summary>The contract is version 1.</summary>
    [Fact]
    public void ContractVersionIsOne() => IOmpAgentAdapter.Version.ShouldBe(1);

    /// <summary>Each of the ten harness event types produces its own kind, and every kind is produced by some type.</summary>
    [Fact]
    public void EveryHarnessEventTypeProducesItsOwnKind()
    {
        (HarnessEvent Payload, OmpEventKind Kind)[] table =
        [
            (new TextDeltaEvent("ok"), OmpEventKind.TextDelta),
            (new ThoughtEvent("reasoning"), OmpEventKind.Thought),
            (new PlanEvent("1. read"), OmpEventKind.Plan),
            (new ToolCallEvent("call-1", "read", "{}"), OmpEventKind.ToolCall),
            (new ToolCallUpdateEvent("call-1", "completed", "{}"), OmpEventKind.ToolCallUpdate),
            (new PermissionRequestEvent("call-1", "bash", "{}", "request-1"), OmpEventKind.PermissionRequest),
            (new UiBlockEvent(new MarkdownBlock { Id = "block-1", Markdown = "**hi**" }), OmpEventKind.UiBlock),
            (new DiagnosticEvent("slow"), OmpEventKind.Diagnostic),
            (new ErrorEvent("failed", false), OmpEventKind.Error),
            (new CompletedEvent("completed"), OmpEventKind.Completed),
        ];

        var log = new OmpEventLog("session-kinds");
        var kinds = new List<OmpEventKind>();
        foreach (var row in table)
        {
            OmpEvent recorded = log.Append(row.Payload, "C-kinds", At);
            recorded.Kind.ShouldBe(row.Kind);
            kinds.Add(recorded.Kind);
        }

        kinds.Distinct().Count().ShouldBe(table.Length);
        kinds.Distinct().Count().ShouldBe(Enum.GetValues<OmpEventKind>().Length);
    }

    /// <summary>An envelope of any other contract version is refused.</summary>
    [Fact]
    public void EnvelopeRefusesAnotherContractVersion() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OmpEvent(2, "session-a", 1, new CompletedEvent("completed"), At, "C-1"));

    /// <summary>Sequences start at 1, so an envelope at sequence 0 is refused.</summary>
    [Fact]
    public void EnvelopeRefusesSequenceBelowOne() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new OmpEvent(IOmpAgentAdapter.Version, "session-a", 0, new CompletedEvent("completed"), At, "C-1"));
}
