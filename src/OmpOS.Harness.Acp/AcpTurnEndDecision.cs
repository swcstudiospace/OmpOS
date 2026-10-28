namespace OmpOS.Harness.Acp;

/// <summary>What a hook gate decided when a prompt turn ended: whether the conversation continues with a follow-up prompt.</summary>
public sealed record AcpTurnEndDecision
{
    /// <summary>Gets the decision that ends the turn as it ended.</summary>
    public static AcpTurnEndDecision End { get; } = new();

    /// <summary>
    /// Gets the text of the follow-up prompt a <c>stop</c> hook asked for, or null when the conversation ends. The session sends it as the next
    /// <c>session/prompt</c> inside the same event stream.
    /// </summary>
    public string? FollowUpPrompt { get; init; }

    /// <summary>Gets the notice to show in the stream when the turn continues, for example <c>Hook continued the conversation (1 of 5)</c>, or null.</summary>
    public string? Notice { get; init; }
}
