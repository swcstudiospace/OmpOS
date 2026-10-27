namespace OmpOS.Contracts.Controls;

/// <summary>
/// The <c>kind</c> discriminators of the stream events that this contract version adds to a session's log. A client that does not know
/// them reads them as <see cref="OmpOS.Contracts.Streaming.UnknownPayload"/> with the kind preserved, so older clients keep working.
/// </summary>
public static class StreamEventKinds
{
    /// <summary>A stage of the session changed. The payload is a <see cref="OmpOS.Contracts.Agents.StageEvent"/> plus the <c>kind</c> member.</summary>
    public const string Stage = "stage";

    /// <summary>An option of the session changed. The payload is a <see cref="ConfigOptionUpdateEvent"/> plus the <c>kind</c> member.</summary>
    public const string ConfigOptionUpdate = "config_option_update";
}
