using OmpOS.Presentation.Composer;

namespace OmpOS.Native.Views;

/// <summary>One value of an option in the list of an <see cref="OptionChipView"/>.</summary>
/// <param name="Choice">The value the engine offers.</param>
/// <param name="IsCurrent">Whether the value is the one in use.</param>
internal sealed record OptionChoiceRow(OptionChoice Choice, bool IsCurrent)
{
    /// <summary>Gets the display name of the value.</summary>
    public string Name => Choice.Name;

    /// <summary>Gets the engine's description of the value, or null.</summary>
    public string? Description => Choice.Description;

    /// <summary>Gets the name a screen reader announces for the row: the value, and that it is the one in use.</summary>
    public string AutomationName => IsCurrent ? $"{Choice.Name}, current" : Choice.Name;
}
