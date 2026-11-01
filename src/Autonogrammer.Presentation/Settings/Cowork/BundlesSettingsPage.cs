using Autonogrammer.Presentation.Settings.Plugins;

namespace Autonogrammer.Presentation.Settings.Cowork;

/// <summary>The plugin bundles page. Bundle items show under the project label. Remote sessions load no bundles.</summary>
public sealed class BundlesSettingsPage : SettingsPageBase
{
    /// <summary>Creates the page over a management source.</summary>
    /// <param name="source">The installer adapter.</param>
    public BundlesSettingsPage(IBundleManagementSource source)
        : base(SettingsSection.Bundles, SettingsCopy.BundlesTitle, SettingsPageState.Loading)
    {
        Model = new BundleSectionModel(source);
    }

    /// <summary>Gets the model the view binds.</summary>
    public BundleSectionModel Model { get; }

    /// <inheritdoc />
    public override Task LoadAsync(CancellationToken cancellationToken) =>
        RunLoadAsync(Model.LoadAsync, cancellationToken);
}
