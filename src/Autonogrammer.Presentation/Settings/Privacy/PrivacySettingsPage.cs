namespace Autonogrammer.Presentation.Settings.Privacy;

/// <summary>
/// The Privacy page: short statements about what Autonogrammer does with your code and prompts. It is read-only, has no source and is
/// ready at once; loading it changes nothing.
/// </summary>
public sealed class PrivacySettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="PrivacySettingsPage"/> class.</summary>
    public PrivacySettingsPage()
        : base(SettingsSection.Privacy, PrivacyCopy.Title, SettingsPageState.Ready)
    {
    }

    /// <summary>Gets the statements in the order the page draws them.</summary>
    public IReadOnlyList<PrivacyRow> Rows { get; } = PrivacyCopy.Rows;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
