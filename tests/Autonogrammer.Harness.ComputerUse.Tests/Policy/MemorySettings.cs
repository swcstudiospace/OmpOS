using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

internal sealed class MemorySettings : IComputerUseSettingsStore
{
    private ComputerUseUserSettings _settings = new();

    public ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_settings);

    public ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken)
    {
        _settings = settings;
        return ValueTask.CompletedTask;
    }
}
