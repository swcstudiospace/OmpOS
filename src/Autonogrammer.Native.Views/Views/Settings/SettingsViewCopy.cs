using System.Globalization;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The few words the Settings window draws that <c>SettingsCopy</c>, <c>ModelsCopy</c> and <c>CustomizeCopy</c> do not supply, and the one
/// rule that joins a label to a provider's name. No view holds a literal; each reads one of these members.
/// </summary>
internal static class SettingsViewCopy
{
    /// <summary>The label of the button that asks the window to close.</summary>
    public const string CloseLabel = "Close";

    /// <summary>The accessible name of the polite live region of the Models page.</summary>
    public const string ModelsAnnouncementName = "Provider changes";

    /// <summary>The accessible name of the polite live region of the window's footer.</summary>
    public const string FooterAnnouncementName = "Settings status";

    /// <summary>The mark drawn beside a navigation entry whose page has unsaved edits. The entry's accessible name says the same in words.</summary>
    public const string DirtyMarker = "\u2022";

    /// <summary>
    /// Joins an action label to the provider it acts on, so two cards that offer the same action have different accessible names.
    /// </summary>
    /// <param name="label">The action label, for example Test connection.</param>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The label, a comma and the provider name.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static string ForProvider(string label, string providerName)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(providerName);
        return string.Create(CultureInfo.InvariantCulture, $"{label}, {providerName}");
    }
}
