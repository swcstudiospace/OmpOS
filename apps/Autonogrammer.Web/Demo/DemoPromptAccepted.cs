namespace Autonogrammer.Web.Demo;

/// <summary>The 202 body of <c>POST /sessions/{id}/prompts</c>, in the shape of the API's <c>PromptAcceptedResponse</c>.</summary>
internal sealed record DemoPromptAccepted
{
    /// <summary>Gets the session.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the stored status after the prompt was accepted.</summary>
    public required string Status { get; init; }

    /// <summary>Gets the intent kind.</summary>
    public required string IntentKind { get; init; }

    /// <summary>Gets the diagnostic codes. The Demo reports none.</summary>
    public required string[] DiagnosticCodes { get; init; }
}
