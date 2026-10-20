using System.Text.Json.Serialization;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// Terminal frame for one admitted prompt, sent once the work the prompt caused has settled. Correlated by
/// <see cref="Id"/>.
/// </summary>
public sealed record PromptResultFrame
{
    /// <summary>Gets the frame discriminator; always <c>prompt_result</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the id of the admitted <c>prompt</c> command.</summary>
    [JsonConverter(typeof(RpcIdConverter))]
    public string? Id { get; init; }

    /// <summary>Gets whether the prompt reached the agent. False for local slash commands and early failures.</summary>
    public bool AgentInvoked { get; init; }

    /// <summary>Gets how the prompt ended: <c>completed</c>, <c>aborted</c> or <c>error</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the failure detail when <see cref="Status"/> is <c>error</c>.</summary>
    public RpcPromptError? Error { get; init; }

    /// <summary>Gets whether the session is quiet. When false, a <c>session_settled</c> frame follows later.</summary>
    public bool SessionSettled { get; init; }
}
