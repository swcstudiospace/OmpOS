using OmpOS.Presentation.Commands.Handlers;

namespace OmpOS.Native.Tests.Display;

/// <summary>A display settings store that keeps the saved values in memory, so a test sees what the model saved.</summary>
internal sealed class FakeDisplaySettingsStore : IDisplaySettingsStore
{
    /// <summary>Gets the values the model saved, oldest first.</summary>
    internal List<DisplaySettingsValues> Saved { get; } = [];

    /// <inheritdoc />
    public Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Saved.Count > 0 ? Saved[^1] : DisplaySettingsValues.Defaults);

    /// <inheritdoc />
    public Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken)
    {
        Saved.Add(values);
        return Task.CompletedTask;
    }
}
