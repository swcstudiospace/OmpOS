namespace OmpOS.Harness.Plugins;

/// <summary>
/// One persisted quarantine.
/// </summary>
internal sealed class QuarantineDocument
{
    public string PluginId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset QuarantinedAt { get; set; }
}
