using OmpOS.Presentation.Settings;

namespace OmpOS.UI.Components.Settings;

/// <summary>
/// A read-only Settings page that explains what a setting is for and where it lives. The web head uses it for the desktop settings it has no
/// control for (remote environments, cloud agents, run mode, workspaces, privacy, the Agents terminal switch): it states the purpose, the facts the
/// web head can vouch for and, when the setting lives elsewhere, the notice that it is configured in the desktop app. It has no draft and is ready at
/// once.
/// </summary>
public sealed class InfoSettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="InfoSettingsPage"/> class.</summary>
    /// <param name="section">The section the page fills.</param>
    /// <param name="title">The navigation label and heading.</param>
    /// <param name="description">The sentence under the heading.</param>
    /// <param name="facts">The headings and paragraphs, in order.</param>
    /// <param name="desktopNotice">The sentence that says the setting is configured in the desktop app, or null when the page has nothing to configure.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="desktopNotice"/> is null.</exception>
    public InfoSettingsPage(SettingsSection section, string title, string description, IReadOnlyList<InfoSettingsFact> facts, string? desktopNotice)
        : base(section, title, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(facts);
        Description = description;
        Facts = [.. facts];
        DesktopNotice = desktopNotice;
    }

    /// <summary>Gets the sentence under the heading.</summary>
    public string Description { get; }

    /// <summary>Gets the headings and paragraphs of the page.</summary>
    public IReadOnlyList<InfoSettingsFact> Facts { get; }

    /// <summary>Gets the sentence that says the setting is configured in the desktop app, or null.</summary>
    public string? DesktopNotice { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
