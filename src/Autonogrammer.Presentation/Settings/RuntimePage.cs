using System.ComponentModel;

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// The Runtime page: idle stop and completed-session retention over a <see cref="RuntimeSettingsModel"/>. The model validates each
/// entry inline; a rejected entry can never be saved. The page has no source: a head persists the values by handling
/// <see cref="Saved"/>, which is raised after every successful save.
/// </summary>
public sealed class RuntimePage : SettingsPageBase
{
    /// <summary>Initializes a new instance of the <see cref="RuntimePage"/> class.</summary>
    /// <param name="model">The Runtime section model, constructed over the previously saved values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public RuntimePage(RuntimeSettingsModel model)
        : base(SettingsSection.Runtime, SettingsCopy.RuntimeTitle, SettingsPageState.Ready)
    {
        ArgumentNullException.ThrowIfNull(model);
        Model = model;
        model.PropertyChanged += OnModelPropertyChanged;
    }

    /// <summary>Raised with the new values after the page is saved, so a head can persist them.</summary>
    public event EventHandler<RuntimeSettingsSavedEventArgs>? Saved;

    /// <summary>Gets the Runtime section model the view binds to.</summary>
    public RuntimeSettingsModel Model { get; }

    /// <inheritdoc/>
    public override bool IsDirty => Model.IsDirty;

    /// <inheritdoc/>
    public override bool HasError => Model.HasErrors;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public override Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        if (!Model.IsDirty)
        {
            return Task.FromResult(true);
        }

        if (Model.TrySave() is not { } values)
        {
            return Task.FromResult(false);
        }

        Saved?.Invoke(this, new RuntimeSettingsSavedEventArgs(values));
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override void Reset() => Model.Reset();

    /// <inheritdoc/>
    public override void Discard() => Model.Discard();

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(RuntimeSettingsModel.IsDirty), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(RuntimeSettingsModel.HasErrors), StringComparison.Ordinal))
        {
            NotifyDirtyChanged();
        }
    }
}
