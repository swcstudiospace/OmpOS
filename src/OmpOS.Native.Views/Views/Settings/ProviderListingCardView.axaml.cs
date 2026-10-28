using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// One provider listing card of the Models page: the provider, its region, kind, country and the vendor's residency statement, its
/// status with the "verify before use" mark, the plan terms of a coding plan, and the setup form with the masked, write-only key field.
/// The <see cref="ProviderListingCardModel"/> decides what is possible; this view only draws it and forwards the three clicks.
/// </summary>
/// <remarks>The typed key lives only in the card's <see cref="ApiKeyDraft"/>. No name, tooltip or message the card draws contains it.</remarks>
internal partial class ProviderListingCardView : UserControl
{
    /// <summary>Creates a card with no model.</summary>
    public ProviderListingCardView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the listing the card shows, or null.</summary>
    internal ProviderListingCardModel? Card => DataContext as ProviderListingCardModel;

    /// <summary>Gets the Set up button.</summary>
    internal Button Setup => SetupButton;

    /// <summary>Gets the Add provider button of the form.</summary>
    internal Button Add => AddButton;

    /// <summary>Gets the Cancel button of the form.</summary>
    internal Button CancelSetup => CancelSetupButton;

    /// <summary>Gets the model id entry.</summary>
    internal TextBox ModelIds => ModelIdsBox;

    /// <summary>Gets the line that shows why the last action failed.</summary>
    internal TextBlock Error => ListingErrorLine;

    /// <summary>Gets the chip that says to verify the provider before use.</summary>
    internal Border Verify => VerifyChip;

    /// <summary>Gets the selectable text of the vendor's plan rule.</summary>
    internal SelectableTextBlock PlanRule => PlanRuleLine;

    /// <summary>Gets the tick box that says the vendor's rule text was read. It is shown for a coding plan only.</summary>
    internal CheckBox PlanRuleTick => PlanRuleBox;

    /// <summary>Gets the line that says key entry is turned off by the operator.</summary>
    internal TextBlock KeyDisabled => KeyDisabledLine;

    private void OnSetupClick(object? sender, RoutedEventArgs e) => Card?.BeginSetup();

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            // A refused save is shown in ErrorText and never thrown, so the task is not observed here.
            _ = card.SaveAsync(CancellationToken.None);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Card?.CancelSetup();
}
