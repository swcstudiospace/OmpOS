namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>
/// What the Browser page edits. Two values are equal when their host lists hold the same entries in the same order.
/// </summary>
/// <param name="ExecutablePath">An explicit Chromium path. Empty means locate.</param>
/// <param name="PolicyMode">The origin policy.</param>
/// <param name="AllowedHosts">Hosts allowed in strict mode.</param>
/// <param name="DeniedHosts">Hosts that are always denied.</param>
/// <param name="DownloadMode">The download policy.</param>
/// <param name="SandboxMode">The sandbox policy.</param>
/// <param name="PersistentProfile">Whether a named profile is kept.</param>
/// <param name="AllowDispatchSessions">Whether dispatch-origin turns may use the browser.</param>
/// <param name="LocalOriginsTrusted">Whether granted local origins are treated as trusted.</param>
public sealed record BrowserUseSettingsValues(
    string ExecutablePath,
    BrowserUsePolicyMode PolicyMode,
    IReadOnlyList<string> AllowedHosts,
    IReadOnlyList<string> DeniedHosts,
    BrowserUseDownloadMode DownloadMode,
    BrowserUseSandboxMode SandboxMode,
    bool PersistentProfile,
    bool AllowDispatchSessions,
    bool LocalOriginsTrusted)
{
    /// <summary>Gets the documented defaults.</summary>
    public static BrowserUseSettingsValues Defaults { get; } = new(
        string.Empty,
        BrowserUsePolicyMode.PromptOnNewOrigin,
        [],
        [],
        BrowserUseDownloadMode.Deny,
        BrowserUseSandboxMode.Required,
        PersistentProfile: false,
        AllowDispatchSessions: false,
        LocalOriginsTrusted: false);

    /// <inheritdoc/>
    public bool Equals(BrowserUseSettingsValues? other) =>
        other is not null
        && string.Equals(ExecutablePath, other.ExecutablePath, StringComparison.Ordinal)
        && PolicyMode == other.PolicyMode
        && DownloadMode == other.DownloadMode
        && SandboxMode == other.SandboxMode
        && PersistentProfile == other.PersistentProfile
        && AllowDispatchSessions == other.AllowDispatchSessions
        && LocalOriginsTrusted == other.LocalOriginsTrusted
        && AllowedHosts.SequenceEqual(other.AllowedHosts, StringComparer.Ordinal)
        && DeniedHosts.SequenceEqual(other.DeniedHosts, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ExecutablePath, PolicyMode, DownloadMode, SandboxMode, AllowedHosts.Count, DeniedHosts.Count);
}
