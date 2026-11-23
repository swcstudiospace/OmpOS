using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Presentation.Settings.BrowserUse;

namespace Autonogrammer.Native.Settings;

/// <summary>Maps between the persisted Browser settings values and the live harness options. One mapping, used by the settings source and the head composition.</summary>
internal static class BrowserUseSettingsPersistence
{
    /// <summary>Applies persisted values to the live options.</summary>
    /// <param name="values">The persisted values.</param>
    /// <param name="options">The live options to fill.</param>
    public static void Apply(BrowserUseSettingsValues values, BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(options);
        options.Enabled = values.Enabled;
        options.ExecutablePath = values.ExecutablePath;
        options.PolicyMode = Enum.Parse<PolicyMode>(values.PolicyMode.ToString());
        options.DownloadMode = Enum.Parse<DownloadMode>(values.DownloadMode.ToString());
        options.SandboxMode = Enum.Parse<SandboxMode>(values.SandboxMode.ToString());
        options.PersistentProfile = values.PersistentProfile;
        options.AllowDispatchSessions = values.AllowDispatchSessions;
        options.LocalOriginsTrusted = values.LocalOriginsTrusted;
        Replace(options.AllowedHosts, values.AllowedHosts);
        Replace(options.DeniedHosts, values.DeniedHosts);
    }

    /// <summary>Reads the persisted values from the live options.</summary>
    /// <param name="options">The live options.</param>
    /// <returns>The values.</returns>
    public static BrowserUseSettingsValues Read(BrowserUseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new BrowserUseSettingsValues(
            options.ExecutablePath,
            Enum.Parse<BrowserUsePolicyMode>(options.PolicyMode.ToString()),
            [.. options.AllowedHosts],
            [.. options.DeniedHosts],
            Enum.Parse<BrowserUseDownloadMode>(options.DownloadMode.ToString()),
            Enum.Parse<BrowserUseSandboxMode>(options.SandboxMode.ToString()),
            options.PersistentProfile,
            options.AllowDispatchSessions,
            options.LocalOriginsTrusted,
            options.Enabled);
    }

    private static void Replace(IList<string> target, IReadOnlyList<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
