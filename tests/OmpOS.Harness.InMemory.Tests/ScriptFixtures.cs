using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.ContractTests;

namespace OmpOS.Harness.InMemory.Tests;

/// <summary>Shared scripts and consumers for the in-memory harness tests.</summary>
internal static class ScriptFixtures
{
    /// <summary>The happy-path stream, including one markdown UI block.</summary>
    public static readonly HarnessEvent[] HappySequence =
    [
        new TextDeltaEvent("Hel"),
        new ThoughtEvent("considering"),
        new ToolCallEvent("call-1", "read", "{\"path\":\"a.txt\"}"),
        new ToolCallUpdateEvent("call-1", "completed", "{}"),
        new PermissionRequestEvent("call-2", "bash", "{\"cmd\":\"ls\"}"),
        new PlanEvent("1. read\n2. answer"),
        new UiBlockEvent(new MarkdownBlock { Id = "block-1", Markdown = "**hi**" }),
        new TextDeltaEvent("lo"),
        new CompletedEvent("end_turn"),
    ];

    /// <summary>Builds a script that emits the given events in order.</summary>
    /// <param name="events">The events to emit.</param>
    /// <param name="capacity">The bounded-channel capacity.</param>
    /// <returns>The script.</returns>
    public static InMemoryScript Emit(IEnumerable<HarnessEvent> events, int capacity = InMemoryScript.DefaultChannelCapacity) =>
        new()
        {
            Steps = events.Select(evt => (InMemoryStep)new EmitStep(evt)).ToArray(),
            ChannelCapacity = capacity,
        };

    /// <summary>Starts a prompt on <paramref name="session"/> and appends its events to <paramref name="seen"/>.</summary>
    /// <param name="session">The session to prompt.</param>
    /// <param name="seen">Receives each event in arrival order, including any delivered before an error.</param>
    /// <param name="ct">Cancels the consumer.</param>
    /// <returns>A task that completes when the stream ends or faults.</returns>
    public static Task ConsumeAsync(IOmpSession session, ICollection<HarnessEvent> seen, CancellationToken ct) =>
        AsyncStreamHelpers.DrainAsync(session.SubmitPromptAsync("prompt", new PromptOptions(), ct), seen, ct);
}
