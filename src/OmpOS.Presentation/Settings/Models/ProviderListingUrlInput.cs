using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>The input of one base URL placeholder on the setup form of a listing.</summary>
public sealed class ProviderListingUrlInput : ObservableModel
{
    private readonly string _providerName;
    private string _value = string.Empty;
    private string? _error;

    /// <summary>Initializes a new instance of the <see cref="ProviderListingUrlInput"/> class.</summary>
    /// <param name="variable">The placeholder.</param>
    /// <param name="providerName">The provider's display name.</param>
    internal ProviderListingUrlInput(ProviderListingVariable variable, string providerName)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(providerName);
        Variable = variable;
        _providerName = providerName;
    }

    /// <summary>Gets the placeholder the input fills.</summary>
    public ProviderListingVariable Variable { get; }

    /// <summary>Gets the placeholder name.</summary>
    public string Name => Variable.Name;

    /// <summary>Gets the label of the input.</summary>
    public string Label => Variable.Label;

    /// <summary>Gets the accessible name of the input.</summary>
    public string FieldName => ProviderListingCopy.VariableFieldName(_providerName, Variable.Label);

    /// <summary>Gets the hint shown in the empty input: the example value, or an empty string.</summary>
    public string Hint => Variable.Example ?? string.Empty;

    /// <summary>Gets the sentence that says where the value is found, or null.</summary>
    public string? Help => Variable.Help;

    /// <summary>Gets or sets the typed value.</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value ?? string.Empty))
            {
                Error = null;
            }
        }
    }

    /// <summary>Gets the problem with the typed value, or null. It is set when the form is submitted and cleared when the value changes.</summary>
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    /// <summary>Checks the typed value and sets <see cref="Error"/>.</summary>
    /// <returns>True when the value is acceptable.</returns>
    internal bool Validate()
    {
        var trimmed = _value.Trim();
        if (trimmed.Length == 0)
        {
            Error = ProviderListingCopy.VariableMissing(Variable.Label);
            return false;
        }

        if (!ProviderListingUrl.IsAcceptable(trimmed))
        {
            Error = ProviderListingCopy.VariableInvalid(Variable.Label);
            return false;
        }

        Error = null;
        return true;
    }
}
