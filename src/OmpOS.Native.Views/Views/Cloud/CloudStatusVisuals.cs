using Avalonia.Data.Converters;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Cloud;

namespace OmpOS.Native.Views.Cloud;

/// <summary>The colour role of a cloud agent status. It reuses the rail's tones, so a cloud row and a chat row of the same kind look the same.</summary>
internal static class CloudStatusVisuals
{
    /// <summary>Converts a <see cref="CloudAgentStatus"/> to the <see cref="StatusTone"/> of its words.</summary>
    public static readonly FuncValueConverter<CloudAgentStatus, StatusTone> Tone = new(ToneOf);

    /// <summary>Gets the tone of a status, through the row status the rail maps it to.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The tone.</returns>
    internal static StatusTone ToneOf(CloudAgentStatus status) => StatusGlyphs.ToneOf(CloudAgentRailMap.Resolve(status).Status);
}
