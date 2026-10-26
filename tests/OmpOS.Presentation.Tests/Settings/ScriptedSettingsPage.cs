using System.ComponentModel;
using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Tests.Settings;

/// <summary>
/// A settings page the test scripts: it is dirty when the test says so, accepts or rejects a save, and counts every call the
/// window makes, so the window protocol is tested without any real page.
/// </summary>
internal sealed class ScriptedSettingsPage : ISettingsPage
{
    private bool _isDirty;
    private bool _hasError;

    /// <summary>Creates a page for a section.</summary>
    /// <param name="section">The section the page fills.</param>
    internal ScriptedSettingsPage(SettingsSection section)
    {
        Section = section;
        Title = section.ToString();
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public SettingsSection Section { get; }

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public bool IsDirty => _isDirty;

    /// <inheritdoc/>
    public bool HasError => _hasError;

    /// <summary>Gets or sets a value indicating whether the next saves are rejected.</summary>
    internal bool RejectSave { get; set; }

    /// <summary>Gets the number of loads.</summary>
    internal int LoadCalls { get; private set; }

    /// <summary>Gets the number of saves.</summary>
    internal int SaveCalls { get; private set; }

    /// <summary>Gets the number of resets.</summary>
    internal int ResetCalls { get; private set; }

    /// <summary>Gets the number of discards.</summary>
    internal int DiscardCalls { get; private set; }

    /// <summary>Marks the page dirty or clean and announces it.</summary>
    /// <param name="dirty">The new state.</param>
    internal void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty)));
    }

    /// <inheritdoc/>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        if (RejectSave)
        {
            _hasError = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
            return Task.FromResult(false);
        }

        SetDirty(false);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public void Reset()
    {
        ResetCalls++;
        SetDirty(true);
    }

    /// <inheritdoc/>
    public void Discard()
    {
        DiscardCalls++;
        _hasError = false;
        SetDirty(false);
    }
}
