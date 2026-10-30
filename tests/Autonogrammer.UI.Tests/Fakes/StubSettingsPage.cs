using System.ComponentModel;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.UI.Tests.Fakes;

/// <summary>A settings page that holds nothing: enough for a settings window model to exist behind the workbench.</summary>
internal sealed class StubSettingsPage : ISettingsPage
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public SettingsSection Section => SettingsSection.General;

    /// <inheritdoc />
    public string Title => "General";

    /// <inheritdoc />
    public bool IsDirty => false;

    /// <inheritdoc />
    public bool HasError => false;

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> SaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    /// <inheritdoc />
    public void Reset()
    {
    }

    /// <inheritdoc />
    public void Discard()
    {
    }
}
