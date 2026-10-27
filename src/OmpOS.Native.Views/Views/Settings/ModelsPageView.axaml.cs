using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The Models page: a search box, the Add provider button, the templates a custom provider can start from, the custom provider form, and
/// the provider cards in their groups. The page is a <see cref="ModelsSettingsPage"/>; every action goes through it or through a card's
/// model. Loading, a failed load with Try again, and the empty list are drawn here and in the shared state view.
/// </summary>
/// <remarks>
/// A template is not a provider omp knows. Choosing one opens the custom provider form filled in; saving writes omp's <c>models.yml</c>
/// (with a backup), and the key is then added on the saved provider's own card.
/// </remarks>
internal partial class ModelsPageView : SettingsPageViewBase
{
    private const string UseTemplateClass = "use-template";

    private bool _draftWasOpen;

    /// <summary>Creates a view with no page.</summary>
    public ModelsPageView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnTemplateButtonClick, RoutingStrategies.Bubble);
        Start();
    }

    /// <summary>Gets the model the view draws, or null.</summary>
    internal ModelsSettingsPage? Models => Page as ModelsSettingsPage;

    /// <summary>Gets the search box.</summary>
    internal TextBox Search => SearchBox;

    /// <summary>Gets the Add provider button.</summary>
    internal Button AddProvider => AddProviderButton;

    /// <summary>Gets the expander that holds the templates.</summary>
    internal Expander Templates => TemplatesExpander;

    /// <summary>Gets the custom provider form.</summary>
    internal CustomProviderFormView Form => DraftForm;

    /// <summary>Gets the Save button of the form.</summary>
    internal Button SaveDraft => SaveDraftButton;

    /// <summary>Gets the Cancel button of the form.</summary>
    internal Button CancelDraft => CancelDraftButton;

    /// <summary>Gets the line that shows why omp refused a save of the form.</summary>
    internal TextBlock DraftError => DraftErrorLine;

    /// <summary>Gets the banner that shows omp's configuration warning.</summary>
    internal Border Warning => WarningBanner;

    /// <summary>Gets the sentence that says the list can be shorter than what omp supports.</summary>
    internal TextBlock Incomplete => IncompleteLine;

    /// <summary>Gets the block shown while omp lists no provider.</summary>
    internal StackPanel Empty => EmptyBlock;

    /// <summary>Gets the line shown while the search keeps no provider.</summary>
    internal TextBlock NoMatches => NoMatchesLine;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Live => AnnouncementLine;

    /// <summary>Gets the block that holds the provider listing.</summary>
    internal StackPanel Listing => ListingBlock;

    /// <summary>Gets the region filter of the provider listing.</summary>
    internal ComboBox RegionChoice => RegionFilter;

    /// <summary>Gets the kind filter of the provider listing.</summary>
    internal ComboBox KindChoice => KindFilter;

    /// <summary>Gets the line shown while the filters keep no listing.</summary>
    internal TextBlock ListingNoMatches => ListingNoMatchesLine;

    /// <summary>Gets the polite live region of the provider listing.</summary>
    internal TextBlock ListingLive => ListingAnnouncementLine;

    /// <summary>Gets the listing cards that are drawn, in page order: section by section.</summary>
    internal IReadOnlyList<ProviderListingCardView> ListingCardViews => [.. ListingSections.GetVisualDescendants().OfType<ProviderListingCardView>()];

    /// <summary>Gets the Use template buttons of the open template list, in list order.</summary>
    internal IReadOnlyList<Button> UseTemplateButtons =>
        [.. TemplateRows.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains(UseTemplateClass))];

    /// <summary>Gets the provider cards that are drawn, in page order: group by group.</summary>
    internal IReadOnlyList<ProviderCardView> CardViews => [.. GroupList.GetVisualDescendants().OfType<ProviderCardView>()];

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is not ModelsSettingsPage models)
        {
            return;
        }

        var draftOpen = models.IsDraftOpen;
        DraftForm.DataContext = models.Draft;
        TemplatesExpander.IsVisible = models.HasTemplates && !draftOpen;
        EmptyBlock.IsVisible = models.State == ModelsPageState.Empty;

        if (draftOpen && !_draftWasOpen)
        {
            DraftForm.DisplayName.FocusWhenReady();
        }
        else if (!draftOpen && _draftWasOpen && FocusIsLost())
        {
            AddProviderButton.FocusWhenReady();
        }

        _draftWasOpen = draftOpen;
    }

    // The focus is lost when the element that had it sat inside the form that just closed.
    private bool FocusIsLost()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || !focused.IsEffectivelyVisible;
    }

    private void OnAddProviderClick(object? sender, RoutedEventArgs e)
    {
        if (Models is not { } page)
        {
            return;
        }

        page.BeginAddCustom();
        if (page.IsDraftOpen)
        {
            DraftForm.DisplayName.FocusWhenReady();
        }
    }

    private void OnSaveDraftClick(object? sender, RoutedEventArgs e)
    {
        if (Models is { } page)
        {
            // A refused save is shown in DraftError and never thrown, so the task is not observed here.
            _ = page.SaveAsync(CancellationToken.None);
        }
    }

    private void OnCancelDraftClick(object? sender, RoutedEventArgs e) => Models?.Discard();

    private void OnTemplateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: ProviderTemplate template } button
            && button.Classes.Contains(UseTemplateClass)
            && Models is { } page)
        {
            page.BeginAddCustomFromTemplate(template.Id);
        }
    }
}
