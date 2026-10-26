using System.ComponentModel;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Tests.Settings.Head;

/// <summary>A page of one section that only counts how often it was loaded. It stands in for the Models page, which the head receives through a factory.</summary>
internal sealed class StubSettingsPage : ISettingsPage
{
    public StubSettingsPage(SettingsSection section)
    {
        Section = section;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public SettingsSection Section { get; }

    /// <inheritdoc />
    public string Title => Section.ToString();

    /// <inheritdoc />
    public bool IsDirty => false;

    /// <inheritdoc />
    public bool HasError => false;

    /// <summary>Gets how many times the page was loaded.</summary>
    public int LoadCount { get; private set; }

    /// <inheritdoc />
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        return Task.CompletedTask;
    }

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
