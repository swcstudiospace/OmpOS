using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Automations;

/// <summary>The body of the webhook receiver's success answers.</summary>
public sealed record AutomationWebhookResponse
{
    /// <summary>Gets what the receiver did: <c>accepted</c> (a run started), <c>ignored</c> (authentic, but no trigger wants it or the automation is off), <c>pong</c> (a GitHub ping).</summary>
    public required string Status { get; init; }

    /// <summary>Gets the identifier of the run that started, for <c>accepted</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunId { get; init; }
}
