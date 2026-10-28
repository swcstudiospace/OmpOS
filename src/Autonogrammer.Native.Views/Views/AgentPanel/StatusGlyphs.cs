using System.Collections.Frozen;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// The glyph table of 46-UI-SPEC: the 16 x 16 path data and the stroke role of each <see cref="StatusIcon"/> key, and the text role of
/// each row status and stage status. The models name a glyph by key; only this type knows how it is drawn.
/// </summary>
internal static class StatusGlyphs
{
    private const string RingData = "M8 2.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11Z";
    private const string AlertData = "M8 2.5 14 13H2ZM8 6.5v3M8 11.2v.1";

    private static readonly FrozenDictionary<string, (string Data, StatusTone Stroke)> Table = new Dictionary<string, (string, StatusTone)>(StringComparer.Ordinal)
    {
        [StatusIcon.Ring] = (RingData, StatusTone.Secondary),
        [StatusIcon.Clock] = (RingData + "M8 5v3.2l2.2 1.3", StatusTone.Secondary),
        [StatusIcon.Running] = ("M8 2.5a5.5 5.5 0 1 0 5.5 5.5", StatusTone.Accent),
        [StatusIcon.Alert] = (AlertData, StatusTone.Warning),
        [StatusIcon.Bubble] = ("M2.5 3.5h11v7h-6l-3 2.5v-2.5h-2Z", StatusTone.Warning),
        [StatusIcon.Paused] = ("M5.5 3.5v9M10.5 3.5v9", StatusTone.Secondary),
        [StatusIcon.Check] = (RingData + "M5.3 8.2 7.2 10 10.8 6", StatusTone.Success),
        [StatusIcon.Cross] = (RingData + "M5.8 5.8l4.4 4.4M10.2 5.8l-4.4 4.4", StatusTone.Error),
        [StatusIcon.Question] = (RingData + "M6.3 6.4a1.8 1.8 0 1 1 2.6 1.6c-.6.4-.9.7-.9 1.4M8 11.4v.1", StatusTone.Warning),
        [StatusIcon.Closed] = (RingData + "M5.5 8h5", StatusTone.Secondary),
        [StatusIcon.Slash] = (RingData + "M4.1 11.9 11.9 4.1", StatusTone.Secondary),
        [StatusIcon.Info] = (RingData + "M8 7.2v3.6M8 5v.1", StatusTone.Secondary),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Gets the converter from a row status to the text role of the status chip.</summary>
    public static readonly FuncValueConverter<AgentRowStatus, StatusTone> RowTone = new(ToneOf);

    /// <summary>Gets the converter from a stage status to the text role of its words.</summary>
    public static readonly FuncValueConverter<StageStatus, StatusTone> StageTone = new(ToneOf);

    /// <summary>Gets whether the key is a glyph of the table.</summary>
    /// <param name="key">The glyph key.</param>
    /// <returns>True when the key names a glyph.</returns>
    internal static bool IsKnown(string? key) => key is not null && Table.ContainsKey(key);

    /// <summary>Parses the path data of a glyph. An unknown key draws the info glyph, so a future key never throws.</summary>
    /// <param name="key">The glyph key, or null.</param>
    /// <returns>A new geometry. It is not cached: geometries belong to the thread that draws them.</returns>
    internal static Geometry GeometryOf(string? key) => Geometry.Parse(Entry(key).Data);

    /// <summary>Returns the stroke role of a glyph.</summary>
    /// <param name="key">The glyph key, or null.</param>
    /// <returns>The role from the glyph table.</returns>
    internal static StatusTone StrokeOf(string? key) => Entry(key).Stroke;

    /// <summary>Returns the text role of a row status, as the status mapping table of 46-UI-SPEC gives it.</summary>
    /// <param name="status">The row status.</param>
    /// <returns>The text role.</returns>
    internal static StatusTone ToneOf(AgentRowStatus status) => status switch
    {
        AgentRowStatus.NeedsApproval or AgentRowStatus.WaitingForYou or AgentRowStatus.Lost => StatusTone.Warning,
        AgentRowStatus.Running or AgentRowStatus.Queued => StatusTone.Primary,
        AgentRowStatus.Completed => StatusTone.Success,
        AgentRowStatus.Failed => StatusTone.Error,
        _ => StatusTone.Secondary,
    };

    /// <summary>Returns the text role of a stage status.</summary>
    /// <param name="status">The stage status. An undefined value reads as quiet text.</param>
    /// <returns>The text role.</returns>
    internal static StatusTone ToneOf(StageStatus status) => status switch
    {
        StageStatus.Running => StatusTone.Primary,
        StageStatus.Succeeded => StatusTone.Success,
        StageStatus.Degraded => StatusTone.Warning,
        StageStatus.Failed => StatusTone.Error,
        _ => StatusTone.Secondary,
    };

    /// <summary>Returns the style class that applies a tone.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>A class name such as <c>tone-warning</c>.</returns>
    internal static string ClassOf(StatusTone tone) => tone switch
    {
        StatusTone.Primary => "tone-primary",
        StatusTone.Success => "tone-success",
        StatusTone.Warning => "tone-warning",
        StatusTone.Error => "tone-error",
        StatusTone.Accent => "tone-accent",
        _ => "tone-secondary",
    };

    private static (string Data, StatusTone Stroke) Entry(string? key) =>
        key is not null && Table.TryGetValue(key, out var entry) ? entry : Table[StatusIcon.Info];
}
