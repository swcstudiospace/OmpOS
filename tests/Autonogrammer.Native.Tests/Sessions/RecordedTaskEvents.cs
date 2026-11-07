using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Native.Tests.Sessions;

/// <summary>
/// Recorded ACP tool-call payloads. Their text is what <c>ToolCallEvent.PayloadJson</c> carries. The matching panel scripts
/// are <c>RecordedTaskFrames</c> in the presentation tests.
/// </summary>
internal static class RecordedTaskEvents
{
    /// <summary>Named task calls: start, output, then a second call whose output arrives with the start.</summary>
    public static IReadOnlyList<HarnessEvent> WithNames { get; } =
    [
        Call("call-1", "task", """{"toolCallId":"call-1","title":"task","kind":"task","status":"in_progress","rawInput":{"agent":"researcher"}}"""),
        Update("call-1", "in_progress", """{"toolCallId":"call-1","title":"task","kind":"task","status":"in_progress","rawInput":{"agent":"researcher"},"rawOutput":{"text":"Searching the repo."}}"""),
        Call("call-2", "task", """{"toolCallId":"call-2","title":"task","kind":"task","status":"in_progress","rawInput":{"name":"writer"},"rawOutput":{"text":"Drafting the note."}}"""),
    ];

    /// <summary>Task calls whose raw input does not name an agent.</summary>
    public static IReadOnlyList<HarnessEvent> WithoutNames { get; } =
    [
        Call("call-a", "task", """{"toolCallId":"call-a","title":"task","kind":"task","status":"in_progress","rawInput":{},"rawOutput":{"text":"first line"}}"""),
        Call("call-b", "task", """{"toolCallId":"call-b","kind":"task","status":"in_progress","content":[{"type":"text","text":"second line"}]}"""),
    ];

    /// <summary>Done, failed and cancelled updates, with one call left running.</summary>
    public static IReadOnlyList<HarnessEvent> StatusTransitions { get; } =
    [
        Call("call-run", "task", """{"toolCallId":"call-run","title":"task","kind":"task","rawInput":{"agent":"runner"}}"""),
        Call("call-done", "task", """{"toolCallId":"call-done","title":"task","kind":"task","rawInput":{"agent":"finisher"}}"""),
        Call("call-fail", "task", """{"toolCallId":"call-fail","title":"task","kind":"task","rawInput":{"agent":"breaker"}}"""),
        Call("call-stop", "task", """{"toolCallId":"call-stop","title":"task","kind":"task","rawInput":{"agent":"halting"}}"""),
        Update("call-done", "completed", """{"toolCallId":"call-done","title":"task","status":"completed","rawOutput":{"text":"finished the note"}}"""),
        Update("call-fail", "failed", """{"toolCallId":"call-fail","title":"task","status":"failed","rawOutput":{"text":"the tool failed"}}"""),
        Update("call-stop", "cancelled", """{"toolCallId":"call-stop","title":"task","status":"cancelled","rawOutput":{"text":"the call was cancelled"}}"""),
    ];

    /// <summary>A read tool call and a text delta. Neither is a task call.</summary>
    public static IReadOnlyList<HarnessEvent> Ignored { get; } =
    [
        Call("read-1", "read", """{"toolCallId":"read-1","title":"read","kind":"read","rawInput":{"path":"notes.txt"}}"""),
        new TextDeltaEvent("not a task"),
        Update("read-1", "completed", """{"toolCallId":"read-1","title":"read","kind":"read","status":"completed"}"""),
    ];

    private static ToolCallEvent Call(string id, string name, string payload) => new(id, name, payload);

    private static ToolCallUpdateEvent Update(string id, string status, string payload) => new(id, status, payload);
}
