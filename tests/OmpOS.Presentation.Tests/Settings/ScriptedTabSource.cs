using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>A Tab source the test scripts: it reports the values it holds, or fails the load.</summary>
internal sealed class ScriptedTabSource : ITabSettingsSource
{
    /// <summary>Gets or sets the status the source reports.</summary>
    internal TabSettingsValues Values { get; set; } = new(Enabled: false, EndpointHost: null, Source: "OMPOS_COMPLETION_ENDPOINT");

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <inheritdoc/>
    public Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure
            ? Task.FromException<TabSettingsValues>(new SettingsSourceException(failure))
            : Task.FromResult(Values);
}
