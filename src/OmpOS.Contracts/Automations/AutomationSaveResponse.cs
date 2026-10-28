using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Automations;

/// <summary>The answer of a create, replace or secret rotation. It is the only place a webhook secret ever appears.</summary>
public sealed record AutomationSaveResponse
{
    /// <summary>Gets the automation after the change.</summary>
    public required AutomationInfo Automation { get; init; }

    /// <summary>Gets the webhook signing secret that this call generated, shown this once. Absent when the call generated none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WebhookSecret { get; init; }
}
