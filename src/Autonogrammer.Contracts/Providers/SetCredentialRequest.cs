using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The body of <c>PUT /providers/{id}/credential</c>. The key is write-only: no response, log or problem repeats it.</summary>
public sealed record SetCredentialRequest
{
    /// <summary>Gets the API key. Required, no whitespace, within 512 characters.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; init; }

    /// <summary>Gets a value indicating whether the user confirmed that they read the vendor's rule text. Required for a coding plan provider.</summary>
    public bool PlanRuleAccepted { get; init; }
}
