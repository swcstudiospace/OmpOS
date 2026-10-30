namespace Autonogrammer.Presentation.Subagents;

/// <summary>
/// Streams row updates for the sub-agent panel. The only implementation in this phase reads ACP <c>tool_call</c> and
/// <c>tool_call_update</c> frames. The RPC <c>get_subagents</c> source is deferred.
/// </summary>
public interface ISubagentActivitySource
{
    /// <summary>Raised when a task call starts or changes.</summary>
    event EventHandler<SubagentActivityUpdateEventArgs>? Updated;
}
