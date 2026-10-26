namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// The only payload keys OmpOS writes to the ledger: an action, an opaque item identifier and an outcome, plus the steering
/// mode and length. A payload never carries a prompt, a body or any operator text.
/// </summary>
public sealed record SubstrateEventPayload
{
    /// <summary>Gets the action, for example <c>steer</c>.</summary>
    public string? Action { get; init; }

    /// <summary>Gets the opaque item identifier.</summary>
    public string? Item { get; init; }

    /// <summary>Gets the outcome, for example <c>queued</c>.</summary>
    public string? Outcome { get; init; }

    /// <summary>Gets the steering mode, <c>steer</c> or <c>follow-up</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets a length in characters, or null.</summary>
    public int? Length { get; init; }
}
