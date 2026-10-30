namespace Autonogrammer.Cowork.Chat;

/// <summary>
/// Where the Cowork permission mode is stored. The values are <see cref="Tools.CoworkPermissionMode"/>.
/// Manual is the default. Skip is a launch mode only when the folder boundary is isolated or the user acknowledged it.
/// </summary>
public static class CoworkPermissionMode
{
    /// <summary>The settings key, <c>cowork.permissionMode</c>.</summary>
    public const string SettingKey = "cowork.permissionMode";

    /// <summary>The mode used when the setting is absent.</summary>
    public static Tools.CoworkPermissionMode Default => Tools.CoworkPermissionMode.Manual;

    /// <summary>Why Skip is not available, or null when the requested mode is not Skip or Skip is available.</summary>
    public const string SkipUnavailableReason = "Skip needs a FilesystemIsolated folder boundary or a per-session acknowledgement.";

    /// <summary>The <c>--approval-mode</c> value. Never <c>yolo</c>.</summary>
    /// <param name="mode">The stored mode.</param>
    /// <returns><c>always-ask-cached</c> for Manual, <c>always-ask</c> for Auto and Skip.</returns>
    public static string ApprovalModeArgument(Tools.CoworkPermissionMode mode) => mode switch
    {
        Tools.CoworkPermissionMode.Manual => "always-ask-cached",
        Tools.CoworkPermissionMode.Auto => "always-ask",
        Tools.CoworkPermissionMode.Skip => "always-ask",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "The Cowork permission mode is not known."),
    };

    /// <summary>The mode the resolver applies. Skip without isolation or acknowledgement is Manual.</summary>
    /// <param name="mode">The stored mode.</param>
    /// <param name="filesystemIsolated">Whether the folder boundary is FilesystemIsolated.</param>
    /// <param name="skipAcknowledged">Whether the user acknowledged Skip for this session.</param>
    /// <returns>The effective mode.</returns>
    public static Tools.CoworkPermissionMode Effective(Tools.CoworkPermissionMode mode, bool filesystemIsolated, bool skipAcknowledged)
    {
        if (mode == Tools.CoworkPermissionMode.Skip && !filesystemIsolated && !skipAcknowledged)
        {
            return Tools.CoworkPermissionMode.Manual;
        }

        return mode;
    }
}
