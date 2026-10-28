using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Automations;

namespace OmpOS.Native.Views.Automations;

/// <summary>
/// The automation editor: name, prompt, schedule with a preview of its next runs, webhook trigger, target, repositories and the limits of a
/// run, and a panel that shows a new webhook secret once. The view makes no manager call itself: every action goes through
/// <see cref="AutomationEditorModel"/>, and Save, Cancel and the stored-the-secret confirmation are raised as events for the page, whose
/// <see cref="HostedAutomationsViewModel"/> reads the list again after a save.
/// </summary>
internal partial class AutomationEditorView : UserControl
{
    private static readonly AutomationChoice[] Flavours =
    [
        new("generic", AutomationsCopy.FlavourGenericLabel),
        new("github", AutomationsCopy.FlavourGithubLabel),
    ];

    private AutomationEditorModel? _model;
    private bool _syncing;
    private bool _secretWasVisible;

    /// <summary>Creates the form with no model.</summary>
    public AutomationEditorView()
    {
        InitializeComponent();
    }

    /// <summary>Occurs when the user asks to save the automation.</summary>
    internal event EventHandler? SaveRequested;

    /// <summary>Occurs when the user asks to close the editor without saving.</summary>
    internal event EventHandler? CancelRequested;

    /// <summary>Occurs when the user confirms that the webhook secret is stored.</summary>
    internal event EventHandler? SecretStored;

    /// <summary>Gets the name field.</summary>
    internal TextBox NameField => NameBox;

    /// <summary>Gets the prompt field.</summary>
    internal TextBox PromptField => PromptBox;

    /// <summary>Gets the cron expression field.</summary>
    internal TextBox CronField => CronBox;

    /// <summary>Gets the Preview next runs button.</summary>
    internal Button Preview => PreviewButton;

    /// <summary>Gets the list of previewed runs.</summary>
    internal ItemsControl PreviewList => CronPreviewList;

    /// <summary>Gets the webhook checkbox.</summary>
    internal CheckBox WebhookEnabledField => WebhookEnabledBox;

    /// <summary>Gets the target pick list.</summary>
    internal ComboBox Target => TargetBox;

    /// <summary>Gets the repositories field.</summary>
    internal TextBox RepositoriesField => RepositoriesBox;

    /// <summary>Gets the Save button.</summary>
    internal Button Save => SaveButton;

    /// <summary>Gets the Cancel button.</summary>
    internal Button Cancel => CancelButton;

    /// <summary>Gets the panel that shows a new webhook secret once.</summary>
    internal Border Secret => SecretPanel;

    /// <summary>Gets the button that confirms the secret is stored.</summary>
    internal Button StoredSecret => StoredSecretButton;

    /// <summary>Moves focus to the first field, the name.</summary>
    internal void FocusFirstField() => NameBox.FocusWhenReady();

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        var model = DataContext as AutomationEditorModel;
        _model = model;
        _secretWasVisible = false;
        if (model is not null)
        {
            model.Changed += OnModelChanged;
            _syncing = true;
            try
            {
                FlavourBox.ItemsSource = Flavours;
                TargetBox.ItemsSource = model.SupportedTargetKinds
                    .Select(static kind => new AutomationChoice(kind, AutomationsCopy.TargetKindText(kind)))
                    .ToArray();
            }
            finally
            {
                _syncing = false;
            }
        }

        Render();
    }

    private void OnModelChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        var model = _model;
        if (model is null)
        {
            return;
        }

        EditorTitle.Text = model.IsEdit ? AutomationsCopy.EditorEditTitle : AutomationsCopy.EditorNewTitle;

        var secretVisible = model.RevealedSecret.Length > 0;
        SecretPanel.IsVisible = secretVisible;
        if (!secretVisible)
        {
            SecretCopiedText.Text = string.Empty;
        }
        else if (!_secretWasVisible)
        {
            CopySecretButton.FocusWhenReady();
        }

        _secretWasVisible = secretVisible;

        WebhookPathText.Text = model.WebhookPath.Length == 0 ? string.Empty : AutomationsCopy.WebhookPathLabel + ": " + model.WebhookPath;
        WebhookPathText.IsVisible = model.WebhookPath.Length > 0;
        RotateButton.IsVisible = model.IsEdit && model.WebhookPath.Length > 0;

        var lines = model.CronPreviewLines;
        if (!ReferenceEquals(CronPreviewList.ItemsSource, lines))
        {
            CronPreviewList.ItemsSource = lines;
        }

        CronPreviewHeading.IsVisible = lines.Count > 0;
        SyncChoices(model);
    }

    private void SyncChoices(AutomationEditorModel model)
    {
        _syncing = true;
        try
        {
            SelectChoice(FlavourBox, model.WebhookFlavour);
            SelectChoice(TargetBox, model.TargetKind);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void SelectChoice(ComboBox box, string value)
    {
        if (box.SelectedItem is AutomationChoice current && string.Equals(current.Value, value, StringComparison.Ordinal))
        {
            return;
        }

        if (box.ItemsSource is IEnumerable<AutomationChoice> choices)
        {
            box.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.Ordinal));
        }
    }

    private void OnFlavourChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _model is not null && FlavourBox.SelectedItem is AutomationChoice choice)
        {
            _model.WebhookFlavour = choice.Value;
        }
    }

    private void OnTargetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _model is not null && TargetBox.SelectedItem is AutomationChoice choice)
        {
            _model.TargetKind = choice.Value;
        }
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        // PreviewCronAsync reports its expected failures in the model and never throws them, so the task is not observed here.
        _ = _model?.PreviewCronAsync(CancellationToken.None);
    }

    private void OnRotateClick(object? sender, RoutedEventArgs e)
    {
        // RotateSecretAsync reports its expected failures in the model and never throws them, so the task is not observed here.
        _ = _model?.RotateSecretAsync(CancellationToken.None);
    }

    private void OnCopySecretClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not { RevealedSecret.Length: > 0 } model || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }

        _ = CopySecretAsync(clipboard, model.RevealedSecret);
    }

    private async Task CopySecretAsync(Avalonia.Input.Platform.IClipboard clipboard, string secret)
    {
        await clipboard.SetTextAsync(secret).ConfigureAwait(true);
        SecretCopiedText.Text = AutomationsCopy.SecretCopied;
    }

    private void OnStoredSecretClick(object? sender, RoutedEventArgs e) => SecretStored?.Invoke(this, EventArgs.Empty);

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);
}
