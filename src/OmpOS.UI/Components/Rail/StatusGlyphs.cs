using System.Collections.Frozen;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.UI.Components.Rail;

/// <summary>
/// The glyph table of the Agent Panel for the web head: the 16 x 16 path data and the colour role of each <see cref="StatusIcon"/> key, and
/// the colour role of the words of a row status and of a stage status. The view models name a glyph by key; only this type knows how it is
/// drawn. A status is never colour alone: every use pairs the glyph with the status words.
/// </summary>
public static class StatusGlyphs
{
    /// <summary>The colour role of the primary (accent) text.</summary>
    public const string TonePrimary = "primary";

    /// <summary>The colour role of quiet text.</summary>
    public const string ToneSecondary = "secondary";

    /// <summary>The colour role of success.</summary>
    public const string ToneSuccess = "success";

    /// <summary>The colour role of a warning.</summary>
    public const string ToneWarning = "warning";

    /// <summary>The colour role of an error.</summary>
    public const string ToneError = "error";

    private const string RingData = "M8 2.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11Z";

    private static readonly FrozenDictionary<string, (string Data, string Tone)> Table = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        [StatusIcon.Ring] = (RingData, ToneSecondary),
        [StatusIcon.Clock] = (RingData + "M8 5v3.2l2.2 1.3", ToneSecondary),
        [StatusIcon.Running] = ("M8 2.5a5.5 5.5 0 1 0 5.5 5.5", TonePrimary),
        [StatusIcon.Alert] = ("M8 2.5 14 13H2ZM8 6.5v3M8 11.2v.1", ToneWarning),
        [StatusIcon.Bubble] = ("M2.5 3.5h11v7h-6l-3 2.5v-2.5h-2Z", ToneWarning),
        [StatusIcon.Paused] = ("M5.5 3.5v9M10.5 3.5v9", ToneSecondary),
        [StatusIcon.Check] = (RingData + "M5.3 8.2 7.2 10 10.8 6", ToneSuccess),
        [StatusIcon.Cross] = (RingData + "M5.8 5.8l4.4 4.4M10.2 5.8l-4.4 4.4", ToneError),
        [StatusIcon.Question] = (RingData + "M6.3 6.4a1.8 1.8 0 1 1 2.6 1.6c-.6.4-.9.7-.9 1.4M8 11.4v.1", ToneWarning),
        [StatusIcon.Closed] = (RingData + "M5.5 8h5", ToneSecondary),
        [StatusIcon.Slash] = (RingData + "M4.1 11.9 11.9 4.1", ToneSecondary),
        [StatusIcon.Info] = (RingData + "M8 7.2v3.6M8 5v.1", ToneSecondary),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Returns the path data of a glyph. An unknown key draws the info glyph, so a future key never throws.</summary>
    /// <param name="key">The glyph key, or null.</param>
    /// <returns>The SVG path data in a 16 x 16 box.</returns>
    public static string PathOf(string? key) => Entry(key).Data;

    /// <summary>Returns the colour role of a glyph.</summary>
    /// <param name="key">The glyph key, or null.</param>
    /// <returns>The role from the glyph table.</returns>
    public static string ToneOf(string? key) => Entry(key).Tone;

    /// <summary>Returns the colour role of a row status.</summary>
    /// <param name="status">The row status.</param>
    /// <returns>The role of the status words.</returns>
    public static string ToneOf(AgentRowStatus status) => status switch
    {
        AgentRowStatus.NeedsApproval or AgentRowStatus.WaitingForYou or AgentRowStatus.Lost => ToneWarning,
        AgentRowStatus.Running or AgentRowStatus.Queued => TonePrimary,
        AgentRowStatus.Completed => ToneSuccess,
        AgentRowStatus.Failed => ToneError,
        _ => ToneSecondary,
    };

    /// <summary>Returns the colour role of a stage status.</summary>
    /// <param name="status">The stage status. An undefined value reads as quiet text.</param>
    /// <returns>The role of the status words.</returns>
    public static string ToneOf(StageStatus status) => status switch
    {
        StageStatus.Running => TonePrimary,
        StageStatus.Succeeded => ToneSuccess,
        StageStatus.Degraded => ToneWarning,
        StageStatus.Failed => ToneError,
        _ => ToneSecondary,
    };

    private static (string Data, string Tone) Entry(string? key) =>
        key is not null && Table.TryGetValue(key, out var entry) ? entry : Table[StatusIcon.Info];
}
