namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// One persisted quarantine lift.
/// </summary>
internal sealed class LiftDocument
{
    public string PluginId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset LiftedAt { get; set; }
}
