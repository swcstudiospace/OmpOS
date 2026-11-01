#pragma warning disable MA0048 // The plan owns this file for the model and its related types.
namespace Autonogrammer.Presentation.Settings.Permissions;

/// <summary>The Cowork permission modes the Permissions page offers. The default is Manual.</summary>
public enum PermissionModeChoice
{
    /// <summary>Ask unless a stored rule already settled the call.</summary>
    Manual = 0,

    /// <summary>Run what the rules and the read-only list allow, and ask otherwise.</summary>
    Auto = 1,

    /// <summary>Allow calls except deletes, after isolation or a one-time acknowledgement.</summary>
    Skip = 2,
}

/// <summary>The mode picker. The label for Auto is "Auto (rules)" and the default stays Manual.</summary>
public sealed class PermissionModesModel
{
    /// <summary>The words for Auto.</summary>
    public const string AutoLabel = "Auto (rules)";

    /// <summary>The words for the default mode.</summary>
    public const string ManualLabel = "Manual";

    /// <summary>The words for Skip.</summary>
    public const string SkipLabel = "Skip";

    /// <summary>The one-time warning shown before Skip.</summary>
    public const string SkipWarning = "Skip runs calls without asking, except deletes. It needs an isolated folder or a one-time acknowledgement for this session.";

    /// <summary>Gets the mode labels in picker order.</summary>
    public IReadOnlyList<string> ModeLabels { get; } = [ManualLabel, AutoLabel, SkipLabel];

    /// <summary>Gets or sets the selected mode. The default is <see cref="PermissionModeChoice.Manual"/>.</summary>
    public PermissionModeChoice Selected { get; set; } = PermissionModeChoice.Manual;

    /// <summary>Gets the label of <see cref="Selected"/>.</summary>
    public string SelectedLabel => Selected switch
    {
        PermissionModeChoice.Manual => ManualLabel,
        PermissionModeChoice.Auto => AutoLabel,
        PermissionModeChoice.Skip => SkipLabel,
        _ => ManualLabel,
    };
}
