namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The limits of the webhook trigger.
/// </summary>
public sealed record WebhookTriggerOptions
{
    /// <summary>The largest body accepted, 256 KiB.</summary>
    public const int MaxBodyBytesCeiling = 256 * 1024;

    /// <summary>Gets the largest body accepted, in bytes, 1 to 262144. Default 262144.</summary>
    public int MaxBodyBytes { get; init; } = MaxBodyBytesCeiling;

    /// <summary>Gets how far a generic delivery's timestamp may be from the server clock, in either direction. Default five minutes.</summary>
    public TimeSpan ReplayWindow { get; init; } = TimeSpan.FromMinutes(5);
}
