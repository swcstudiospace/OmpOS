using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Composer;

/// <summary>One line of the model picker list: a provider heading, or a model under it.</summary>
public sealed class ModelPickerRow : ObservableModel
{
    private bool _isHighlighted;

    private ModelPickerRow(string text, string automationName, ModelChoice? choice, bool isCurrent)
    {
        Text = text;
        AutomationName = automationName;
        Choice = choice;
        IsCurrent = isCurrent;
    }

    /// <summary>Gets the text of the row: the provider name of a heading, or the model's display name.</summary>
    public string Text { get; }

    /// <summary>Gets the name a screen reader announces for the row.</summary>
    public string AutomationName { get; }

    /// <summary>Gets the model the row selects, or <see langword="null"/> for a provider heading.</summary>
    public ModelChoice? Choice { get; }

    /// <summary>Gets a value indicating whether the row is a provider heading. A heading is not selectable.</summary>
    public bool IsHeading => Choice is null;

    /// <summary>Gets a value indicating whether the row is the model the session uses now.</summary>
    public bool IsCurrent { get; }

    /// <summary>Gets a value indicating whether the keyboard cursor of the list is on the row. Only a model row is ever highlighted.</summary>
    public bool IsHighlighted { get => _isHighlighted; internal set => SetProperty(ref _isHighlighted, value); }

    /// <summary>Creates the heading row of a provider.</summary>
    /// <param name="provider">The provider name.</param>
    /// <returns>The heading row.</returns>
    internal static ModelPickerRow Heading(string provider) => new(provider, $"Provider {provider}", null, isCurrent: false);

    /// <summary>Creates the row of one model.</summary>
    /// <param name="choice">The model.</param>
    /// <param name="isCurrent">Whether it is the model in use.</param>
    /// <returns>The model row.</returns>
    internal static ModelPickerRow ForChoice(ModelChoice choice, bool isCurrent) =>
        new(choice.Name, isCurrent ? $"{choice.Name}, {choice.Provider}, current model" : $"{choice.Name}, {choice.Provider}", choice, isCurrent);
}
