using Autonogrammer.Presentation.Dispatch;

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The Dispatch section of the settings window: a page that holds the <see cref="DispatchRelayModel"/> its view draws. The window loads it once
/// when the window loads. The page stays Ready, so the view's own loading, failure and not-configured states show rather than a page-level state.
/// </summary>
public sealed class DispatchSettingsPage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="DispatchSettingsPage"/> class.</summary>
    /// <param name="relay">The model the view draws.</param>
    /// <exception cref="ArgumentNullException"><paramref name="relay"/> is null.</exception>
    public DispatchSettingsPage(DispatchRelayModel relay)
        : base(SettingsSection.Dispatch, DispatchCopy.PageHeading, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(relay);
        Relay = relay;
    }

    /// <summary>Gets the model the view draws.</summary>
    public DispatchRelayModel Relay { get; }

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Relay.LoadAsync(cancellationToken);
}
