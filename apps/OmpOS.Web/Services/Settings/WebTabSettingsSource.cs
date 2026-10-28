using OmpOS.Presentation.Settings;

namespace OmpOS.Web.Services.Settings;

/// <summary>The <see cref="ITabSettingsSource"/> of the web head: tab completion runs in the desktop editor only, so the page reports it off and says why.</summary>
internal sealed class WebTabSettingsSource : ITabSettingsSource
{
    /// <summary>The sentence the page shows as the source of the setting.</summary>
    internal const string Reason = "Tab completion is part of the desktop editor. The web head has no editor.";

    /// <inheritdoc />
    public Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new TabSettingsValues(Enabled: false, EndpointHost: null, Source: Reason));
}
