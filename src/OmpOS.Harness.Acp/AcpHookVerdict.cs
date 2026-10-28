namespace OmpOS.Harness.Acp;

/// <summary>
/// What a hook gate decided about one action the ACP session is about to take or to allow. A denial carries the messages the
/// hooks gave; the session turns them into the JSON-RPC error text or the reject answer and a diagnostic for the turn.
/// </summary>
public sealed record AcpHookVerdict
{
    /// <summary>Gets the verdict that lets everything through. It is what a gate with no applicable hook returns.</summary>
    public static AcpHookVerdict Allow { get; } = new();

    /// <summary>Gets a value indicating whether a hook denied the action.</summary>
    public bool Denied { get; init; }

    /// <summary>Gets the message the hooks asked to show the user, or null.</summary>
    public string? UserMessage { get; init; }

    /// <summary>Gets the message the hooks want the agent to see when the action is denied, or null.</summary>
    public string? AgentMessage { get; init; }

    /// <summary>
    /// Gets the context the hooks added (for example the <c>additional_context</c> of a <c>sessionStart</c> hook). The session puts it in
    /// front of the prompt it belongs to. Only <see cref="IAcpHookGate.BeforePromptAsync"/> uses it.
    /// </summary>
    public string? PromptContext { get; init; }

    /// <summary>Gets the text a denial is reported with: the agent message, else the user message, else a fixed sentence.</summary>
    public string DenialText =>
        !string.IsNullOrWhiteSpace(AgentMessage) ? AgentMessage
        : !string.IsNullOrWhiteSpace(UserMessage) ? UserMessage
        : "A hook denied this action.";
}
