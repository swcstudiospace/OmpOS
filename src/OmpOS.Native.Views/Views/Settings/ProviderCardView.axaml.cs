using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// One provider card of the Models page: the provider's name and status, its endpoint, the models with their default badge or Use as
/// default action, the masked API key entry, Test connection with its outcome, Edit and Remove (with the inline question) and, for a coding
/// plan, the quota lines, validity and plan address as selectable text. The <see cref="ProviderCardModel"/> decides what is possible: an
/// action it does not offer has no visible control.
/// </summary>
/// <remarks>
/// The typed key lives only in the card's <see cref="ApiKeyDraft"/>; the field is masked, has no reveal control, and its text is empty
/// again as soon as a save attempt ends. No name, tooltip or message the card draws contains it.
/// </remarks>
internal partial class ProviderCardView : UserControl
{
    private const string UseDefaultClass = "use-default";
    private const string ToneSuccess = "tone-success";
    private const string ToneError = "tone-error";
    private const string ToneSecondary = "tone-secondary";

    private readonly ModelWatcher<ProviderCardModel> _watch;
    private bool _attached;
    private bool _editorWasOpen;
    private bool _confirmWasOpen;

    /// <summary>Creates a card with no model.</summary>
    public ProviderCardView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<ProviderCardModel>(Refresh);
        AddHandler(Button.ClickEvent, OnRowButtonClick, RoutingStrategies.Bubble);
        ApiKeyBox.AddHandler(KeyDownEvent, OnApiKeyKeyDown, RoutingStrategies.Tunnel);
        Refresh();
    }

    /// <summary>Gets the provider the card shows, or null.</summary>
    internal ProviderCardModel? Card => DataContext as ProviderCardModel;

    /// <summary>Gets the Add or Replace API key button.</summary>
    internal Button ApiKey => ApiKeyButton;

    /// <summary>Gets the masked key entry.</summary>
    internal TextBox ApiKeyEntry => ApiKeyBox;

    /// <summary>Gets the Save key button.</summary>
    internal Button SaveKey => SaveKeyButton;

    /// <summary>Gets the Test connection button.</summary>
    internal Button Test => TestButton;

    /// <summary>Gets the Edit button.</summary>
    internal Button Edit => EditButton;

    /// <summary>Gets the Remove button.</summary>
    internal Button Remove => RemoveButton;

    /// <summary>Gets the button that confirms the removal.</summary>
    internal Button ConfirmRemove => ConfirmRemoveButton;

    /// <summary>Gets the button that keeps the provider.</summary>
    internal Button Keep => KeepButton;

    /// <summary>Gets the line that shows the outcome of the last connection test.</summary>
    internal TextBlock TestResult => TestResultLine;

    /// <summary>Gets the line that shows why the last action failed.</summary>
    internal TextBlock Error => ErrorLine;

    /// <summary>Gets the line that shows what the key entry found wrong with the typed text.</summary>
    internal TextBlock KeyProblem => ApiKeyProblem;

    /// <summary>Gets the selectable text of the plan address.</summary>
    internal SelectableTextBlock PlanUrl => PlanUrlLine;

    /// <summary>Gets the question of the removal confirmation.</summary>
    internal TextBlock RemoveQuestion => RemoveQuestionLine;

    /// <summary>Gets the Use as default buttons of the model rows, in row order.</summary>
    internal IReadOnlyList<Button> UseAsDefaultButtons =>
        [.. ModelRows.GetVisualDescendants().OfType<Button>().Where(static button => button.Classes.Contains(UseDefaultClass))];

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(Card);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            _watch.Set(Card);
        }

        Refresh();
    }

    private void Refresh()
    {
        if (Card is not { } card)
        {
            ApiKeyEditor.DataContext = null;
            return;
        }

        var provider = card.DisplayName;
        ApiKeyEditor.DataContext = card.ApiKey;
        ApiKeyEditor.IsVisible = card.IsApiKeyEditorOpen;
        ApiKeyButton.IsVisible = card.CanSetApiKey && !card.IsApiKeyEditorOpen;
        ApiKeyBox.IsEnabled = card.IsKeyEnabled;
        ApiKeyDisabledLine.IsVisible = card.IsKeyDisabled;
        ApiKeyDisabledLine.Text = card.KeyDisabledReason ?? string.Empty;
        AutomationProperties.SetName(ApiKeyDisabledLine, card.KeyDisabledReason ?? string.Empty);
        ApiKeyButton.IsEnabled = !card.IsBusy;
        TestButton.IsEnabled = !card.IsTesting;
        EditButton.IsEnabled = !card.IsBusy;
        RemoveButton.IsVisible = card.CanRemove && !card.IsConfirmingRemove;
        RemoveButton.IsEnabled = !card.IsBusy;
        ConfirmRemoveButton.IsEnabled = !card.IsBusy;
        NoModelsLine.IsVisible = card.Models.Count == 0;

        PlanUrlLine.IsVisible = card.PlanUrl is not null;
        var planUrl = card.PlanUrl?.ToString() ?? string.Empty;
        PlanUrlLine.Text = planUrl;
        AutomationProperties.SetName(PlanUrlLine, planUrl);

        AutomationProperties.SetName(ApiKeyButton, SettingsViewCopy.ForProvider(card.ApiKeyActionLabel, provider));
        AutomationProperties.SetName(SaveKeyButton, SettingsViewCopy.ForProvider(ModelsCopy.SaveKeyLabel, provider));
        AutomationProperties.SetName(CancelKeyButton, SettingsViewCopy.ForProvider(ModelsCopy.CancelLabel, provider));
        AutomationProperties.SetName(TestButton, SettingsViewCopy.ForProvider(ModelsCopy.TestLabel, provider));
        AutomationProperties.SetName(EditButton, SettingsViewCopy.ForProvider(ModelsCopy.EditLabel, provider));
        AutomationProperties.SetName(RemoveButton, SettingsViewCopy.ForProvider(ModelsCopy.RemoveLabel, provider));
        AutomationProperties.SetName(ConfirmRemoveButton, SettingsViewCopy.ForProvider(ModelsCopy.ConfirmRemoveLabel, provider));
        AutomationProperties.SetName(KeepButton, SettingsViewCopy.ForProvider(ModelsCopy.KeepLabel, provider));

        StatusGlyph.IconKey = CredentialIconOf(card.Info.Credential);
        ApplyTestTone(card.TestStatus);
        MoveFocus(card);
    }

    // The glyph of the credential chip: a check when omp holds a key, an alert when it holds none, an i when the source could not tell,
    // a ring when the provider needs no key.
    private static string CredentialIconOf(CredentialState credential) => credential switch
    {
        CredentialState.Present => StatusIcon.Check,
        CredentialState.Absent => StatusIcon.Alert,
        CredentialState.NotRequired => StatusIcon.Ring,
        _ => StatusIcon.Info,
    };

    private void ApplyTestTone(ProviderTestStatus? status)
    {
        TestResultLine.Classes.Remove(ToneSuccess);
        TestResultLine.Classes.Remove(ToneError);
        TestResultLine.Classes.Remove(ToneSecondary);
        switch (status)
        {
            case ProviderTestStatus.Reachable:
                TestResultLine.Classes.Add(ToneSuccess);
                break;
            case ProviderTestStatus.NotTestable:
                TestResultLine.Classes.Add(ToneSecondary);
                break;
            case null:
                break;
            default:
                TestResultLine.Classes.Add(ToneError);
                break;
        }
    }

    // Focus follows the entry the user opened: into the key field when it opens and into the safe choice of a removal question, and back
    // to the button that opened them when they close and the focus would otherwise be lost.
    private void MoveFocus(ProviderCardModel card)
    {
        var editorOpen = card.IsApiKeyEditorOpen;
        var confirmOpen = card.IsConfirmingRemove;
        var editorOpened = editorOpen && !_editorWasOpen;
        var editorClosed = !editorOpen && _editorWasOpen;
        var confirmOpened = confirmOpen && !_confirmWasOpen;
        var confirmClosed = !confirmOpen && _confirmWasOpen;
        _editorWasOpen = editorOpen;
        _confirmWasOpen = confirmOpen;

        if (!_attached)
        {
            return;
        }

        if (editorOpened)
        {
            ApiKeyBox.FocusWhenReady();
        }
        else if (editorClosed && FocusIsLost(ApiKeyEditor))
        {
            ApiKeyButton.FocusWhenReady();
        }
        else if (confirmOpened)
        {
            KeepButton.FocusWhenReady();
        }
        else if (confirmClosed && FocusIsLost(RemoveConfirm))
        {
            (RemoveButton.IsVisible ? RemoveButton : TestButton).FocusWhenReady();
        }
    }

    // The focus is lost when nothing has it or the element that has it sits inside the part that just closed.
    private bool FocusIsLost(Visual closedPart)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || ReferenceEquals(focused, closedPart) || focused.GetVisualAncestors().Contains(closedPart);
    }

    private void OnRowButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: ProviderModelRow row } button
            && button.Classes.Contains(UseDefaultClass)
            && Card is { } card)
        {
            // The card reports a refused change through its own error text and never throws, so the task is not observed here.
            _ = card.UseAsDefaultAsync(row.Selector, CancellationToken.None);
        }
    }

    private void OnApiKeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (Card is not { } card)
        {
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = card.SaveApiKeyAsync(CancellationToken.None);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            card.CancelApiKey();
        }
    }

    private void OnApiKeyClick(object? sender, RoutedEventArgs e) => Card?.BeginApiKey();

    private void OnSaveKeyClick(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            _ = card.SaveApiKeyAsync(CancellationToken.None);
        }
    }

    private void OnCancelKeyClick(object? sender, RoutedEventArgs e) => Card?.CancelApiKey();

    private void OnTestClick(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            _ = card.TestAsync(CancellationToken.None);
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e) => Card?.BeginEdit();

    private void OnRemoveClick(object? sender, RoutedEventArgs e) => Card?.BeginRemove();

    private void OnConfirmRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (Card is { } card)
        {
            _ = card.ConfirmRemoveAsync(CancellationToken.None);
        }
    }

    private void OnKeepClick(object? sender, RoutedEventArgs e) => Card?.CancelRemove();
}
