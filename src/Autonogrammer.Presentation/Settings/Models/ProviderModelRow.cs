using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>One model on a provider card, with whether it is the default for new chats.</summary>
public sealed class ProviderModelRow : ObservableModel
{
    private bool _isDefault;

    /// <summary>Initializes a new instance of the <see cref="ProviderModelRow"/> class.</summary>
    /// <param name="selector">The model as <c>provider/model</c>.</param>
    /// <param name="isDefault">True when the model is the default.</param>
    internal ProviderModelRow(string selector, bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(selector);
        Selector = selector;
        _isDefault = isDefault;
    }

    /// <summary>Gets the model as <c>provider/model</c>. It is also the text shown for the row.</summary>
    public string Selector { get; }

    /// <summary>Gets a value indicating whether the model is the default for new chats.</summary>
    public bool IsDefault
    {
        get => _isDefault;
        internal set
        {
            if (SetProperty(ref _isDefault, value))
            {
                NotifyPropertyChanged(nameof(CanUseAsDefault));
            }
        }
    }

    /// <summary>Gets a value indicating whether the Use as default action applies: the model is not the default already.</summary>
    public bool CanUseAsDefault => !_isDefault;

    /// <summary>Gets the accessible name of the Use as default action.</summary>
    public string UseAsDefaultName => ModelsCopy.UseAsDefaultName(Selector);
}
