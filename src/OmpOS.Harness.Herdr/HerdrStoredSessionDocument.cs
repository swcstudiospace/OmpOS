namespace OmpOS.Harness.Herdr;

/// <summary>
/// One persisted herdr session entry. The state is stored as its enum name.
/// </summary>
internal sealed class HerdrStoredSessionDocument
{
    public string SessionId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string? NativeReference { get; set; }

    public string LastKnownState { get; set; } = string.Empty;

    public long LastSequence { get; set; }
}
