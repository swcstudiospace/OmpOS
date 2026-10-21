namespace OmpOS.Api.Endpoints;

/// <summary>
/// Response body for an accepted <c>POST /sessions/{id}/prompts</c> (HTTP 202).
/// </summary>
internal sealed record PromptAcceptedResponse
{
    /// <summary>Gets the session the prompt was accepted on.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the acceptance status.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the parser intent-kind name assigned to the prompt (for example <c>Steer</c> or <c>Fallback</c>).</summary>
    public required string IntentKind { get; init; }

    /// <summary>Gets the parser diagnostic codes recorded with the prompt.</summary>
    public required string[] DiagnosticCodes { get; init; }
}
