using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Cloud;

namespace OmpOS.Native.Views.Cloud;

/// <summary>
/// The hand-off dialog content: the typed task (read-only), a repository address, the cost confirmation, Hand off and Cancel. Hand off is enabled
/// by the model only for a valid repository and a ticked cost confirmation. Enter in the repository field starts only when that holds and Esc cancels.
/// The view makes no host call itself; every action goes through <see cref="CloudHandoffModel"/>.
/// </summary>
internal partial class CloudHandoffView : UserControl
{
    private CloudHandoffModel? _model;

    /// <summary>Creates the view with no model.</summary>
    public CloudHandoffView()
    {
        InitializeComponent();

        // Enter and Esc are handled while they tunnel down, before the text box or checkbox can use them.
        RepositoryBox.AddHandler(KeyDownEvent, OnRepositoryKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Gets the read-only task text.</summary>
    internal TextBox TaskField => TaskBox;

    /// <summary>Gets the repository address field.</summary>
    internal TextBox RepositoryField => RepositoryBox;

    /// <summary>Gets the cost confirmation checkbox.</summary>
    internal CheckBox CostConfirm => CostConfirmBox;

    /// <summary>Gets the cost sentence.</summary>
    internal TextBlock CostNotice => CostNoticeText;

    /// <summary>Gets the Hand off button.</summary>
    internal Button Start => StartButton;

    /// <summary>Gets the Cancel button.</summary>
    internal Button Cancel => CancelButton;

    /// <summary>Gets the text that says why Hand off is unavailable.</summary>
    internal TextBlock WhyDisabled => WhyDisabledText;

    /// <summary>Gets the repository problem text.</summary>
    internal TextBlock RepositoryError => RepositoryErrorText;

    /// <summary>Gets the status of the latest attempt.</summary>
    internal TextBlock Status => StatusText;

    /// <summary>Moves focus to the repository field.</summary>
    internal void FocusFirstField() => RepositoryBox.FocusWhenReady();

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as CloudHandoffModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
        }

        RenderState();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusFirstField();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CloudHandoffModel.IsBusy) or nameof(CloudHandoffModel.HasError) or nameof(CloudHandoffModel.Status))
        {
            RenderState();
        }
    }

    private void RenderState()
    {
        var busy = _model?.IsBusy == true;
        var error = _model?.HasError == true;
        StartButton.Content = busy ? CloudCopy.HandoffStartingLabel : CloudCopy.HandoffStartLabel;
        StatusText.Classes.Set("tone-error", error);
        AutomationProperties.SetLiveSetting(StatusText, error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    private void OnRepositoryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _model is { CanStart: true } model)
        {
            e.Handled = true;
            Begin(model);
        }
    }

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _model is { } model)
        {
            e.Handled = true;
            model.Cancel();
        }
    }

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { } model)
        {
            Begin(model);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _model?.Cancel();

    // StartAsync reports its expected failures in the model's result and never throws them, so the task is not observed here.
    private static void Begin(CloudHandoffModel model) => _ = model.StartAsync(CancellationToken.None);
}
