using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings;

/// <summary>A scripted source of the Tab page. It has no save.</summary>
internal sealed class FakeTabSource : FakeSettingsSource<TabSettingsValues>, ITabSettingsSource
{
    /// <summary>Creates a source that reports inline completion on, through a local endpoint.</summary>
    public FakeTabSource()
        : base(new TabSettingsValues(Enabled: true, EndpointHost: "localhost:8080", Source: "OMPOS_COMPLETION_URL"))
    {
    }

    /// <inheritdoc />
    public Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);
}
