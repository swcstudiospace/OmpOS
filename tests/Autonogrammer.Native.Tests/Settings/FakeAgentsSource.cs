using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A scripted source of the Agents page.</summary>
internal sealed class FakeAgentsSource : FakeSettingsSource<AgentsSettingsValues>, IAgentsSettingsSource
{
    /// <summary>Creates a source whose saved auto-run switch is off.</summary>
    public FakeAgentsSource()
        : base(new AgentsSettingsValues(TerminalAutoRun: false))
    {
    }

    /// <inheritdoc />
    public Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken) => SaveCoreAsync(values);
}
