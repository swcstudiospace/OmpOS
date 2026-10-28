namespace OmpOS.Contracts.Hooks;

/// <summary>
/// The <c>kind</c> discriminator of the stream event that reports one hook run. A client that does not know it reads the event as
/// <see cref="OmpOS.Contracts.Streaming.UnknownPayload"/> with the kind preserved, so older clients keep working.
/// </summary>
public static class HookStreamKinds
{
    /// <summary>A hook ran, was skipped or was refused. The payload is a <see cref="HookRunEvent"/> plus the <c>kind</c> member.</summary>
    public const string Hook = "hook";
}
