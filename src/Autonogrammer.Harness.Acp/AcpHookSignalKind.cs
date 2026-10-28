namespace Autonogrammer.Harness.Acp;

/// <summary>The kind of an observation the ACP session reports to its hook gate. None of them can block anything: the work already happened.</summary>
public enum AcpHookSignalKind
{
    /// <summary>A tool call was announced (<c>session/update</c> kind <c>tool_call</c>).</summary>
    ToolCall,

    /// <summary>A tool call progressed or finished (<c>tool_call_update</c>).</summary>
    ToolCallUpdate,

    /// <summary>An assistant message ended; its chunks were aggregated into <see cref="AcpHookSignal.Text"/>.</summary>
    AgentResponse,

    /// <summary>A thinking block ended; its chunks were aggregated into <see cref="AcpHookSignal.Text"/>.</summary>
    AgentThought,

    /// <summary>The agent published or changed its plan. <see cref="AcpHookSignal.Text"/> holds it as a markdown checklist.</summary>
    Plan,
}
