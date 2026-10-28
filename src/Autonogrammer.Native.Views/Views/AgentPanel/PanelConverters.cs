using Avalonia;
using Avalonia.Data.Converters;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>Value converters the Agent Panel views bind with. Each is a pure function of its input.</summary>
internal static class PanelConverters
{
    private const double IndentPerLevel = 16;
    private const string FixMarker = "git submodule update";

    /// <summary>Converts a count to whether it is zero.</summary>
    public static readonly FuncValueConverter<int, bool> IsZero = new(static count => count == 0);

    /// <summary>Converts the nesting depth of a stage row to its left indent, <c>Space.Md</c> per level.</summary>
    public static readonly FuncValueConverter<int, Thickness> DepthIndent = new(static depth => new Thickness(Math.Max(depth, 0) * IndentPerLevel, 0, 0, 0));

    /// <summary>Converts a stage row to whether its detail is a fix command, which is drawn selectable in the mono face.</summary>
    public static readonly FuncValueConverter<StageRow?, bool> IsFixLine = new(static row => IsFix(row));

    /// <summary>Converts a stage row to whether its detail is plain text.</summary>
    public static readonly FuncValueConverter<StageRow?, bool> IsPlainDetail =
        new(static row => row is { Detail.Length: > 0 } && !IsFix(row));

    private static bool IsFix(StageRow? row) =>
        row is { Detail: { } detail } && detail.Contains(FixMarker, StringComparison.Ordinal);
}
