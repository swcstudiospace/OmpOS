namespace OmpOS.Harness.Agent;

/// <summary>
/// The kind of an <see cref="OmpEvent"/>. Each kind names exactly one harness event type. The envelope derives the kind
/// from its payload, so the two cannot disagree.
/// </summary>
public enum OmpEventKind
{
    /// <summary>An incremental piece of assistant text. Payload: TextDeltaEvent.</summary>
    TextDelta,

    /// <summary>An incremental piece of assistant reasoning. Payload: ThoughtEvent.</summary>
    Thought,

    /// <summary>The engine published or updated its plan. Payload: PlanEvent.</summary>
    Plan,

    /// <summary>A tool invocation started. Payload: ToolCallEvent.</summary>
    ToolCall,

    /// <summary>A tool invocation progressed or finished. Payload: ToolCallUpdateEvent.</summary>
    ToolCallUpdate,

    /// <summary>The engine requests permission for a tool call. Payload: PermissionRequestEvent.</summary>
    PermissionRequest,

    /// <summary>A Generative UI block to render. Payload: UiBlockEvent.</summary>
    UiBlock,

    /// <summary>A non-terminal diagnostic. Payload: DiagnosticEvent.</summary>
    Diagnostic,

    /// <summary>The terminal event of a failed turn. Payload: ErrorEvent.</summary>
    Error,

    /// <summary>The terminal event of a finished or cancelled turn. Payload: CompletedEvent.</summary>
    Completed,
}
