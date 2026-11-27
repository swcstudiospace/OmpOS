using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A scripted source of the Agents page.</summary>
internal sealed class FakeAgentsSource : FakeSettingsSource<AgentsSettingsValues>, IAgentsSettingsSource
{
    /// <summary>Creates a source whose saved values are the defaults.</summary>
    public FakeAgentsSource()
        : base(AgentsSettingsValues.Defaults)
    {
    }

    /// <inheritdoc />
    public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken) => SaveCoreAsync(values);
}
