using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OmpOS.Native.Views.Hooks;

/// <summary>
/// The hooks panel: the hooks in run order with their trigger, exact command, enabled and consent state, and a degraded marker
/// for each hook that cannot run. Each row offers Enable, Disable, Move up, Move down and Review consent. The consent panel
/// shows the selected hook's exact argument list and working directory, and its Approve button records consent for that
/// command and nothing else. The panel starts no process.
/// </summary>
/// <remarks>
/// The view draws exactly one state at a time: loading, empty, error, or the list (success or degraded). The rows are rebuilt on
/// every change, so the focused action is looked up again by its accessible name once the new rows are realized.
/// </remarks>
internal partial class HooksView : UserControl
{
    private HooksModel? _model;

    /// <summary>Creates the panel with no data context.</summary>
    public HooksView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the panel to the model in the data context, starts loading the registry, and draws the current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        _model = DataContext as HooksModel;
        ApproveButton.Command = _model?.ApproveCommand;

        if (_model is not null)
        {
            _model.Changed += OnModelChanged;
            // LoadAsync reports its failures in the model's state and never faults, so the task is not observed here.
            _ = _model.LoadAsync(CancellationToken.None);
        }

        Render();
    }

    private void OnModelChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        var focused = FocusedActionName();
        var state = _model?.State ?? HookViewState.Loading;

        StateHeading.Text = HeadingFor(state);
        LoadingText.IsVisible = state == HookViewState.Loading;
        EmptyText.IsVisible = state == HookViewState.Empty;
        ErrorText.IsVisible = state == HookViewState.Error;
        ErrorText.Text = state == HookViewState.Error
            ? "Hooks could not be loaded. Reason: " + _model?.LoadFailure
            : string.Empty;

        var listed = state is HookViewState.Success or HookViewState.Degraded;
        ListPanel.IsVisible = listed;
        HookRows.ItemsSource = listed ? _model?.Rows : null;
        SummaryText.Text = SummaryFor(state);

        var failure = _model?.ActionFailure ?? string.Empty;
        ActionErrorText.IsVisible = failure.Length > 0;
        ActionErrorText.Text = failure.Length > 0 ? "The last change failed. Reason: " + failure : string.Empty;

        RenderConsent();
        RestoreFocusLater(focused);
    }

    private void RenderConsent()
    {
        var selected = _model?.Selected;
        if (selected is null)
        {
            ConsentPrompt.Text = "Choose Review consent on a hook to see its exact command before approving.";
            ConsentDetails.IsVisible = false;
            AutomationProperties.SetName(ApproveButton, "Approve consent");
            return;
        }

        ConsentPrompt.Text = "Approve records consent for this exact command only. It does not run the hook.";
        ConsentTrigger.Text = selected.TriggerText + ". " + selected.ConsentText + ".";
        ConsentDirectory.Text = selected.WorkingDirectoryText;
        ConsentArguments.Text = selected.ArgumentListText;
        ConsentDetails.IsVisible = true;
        AutomationProperties.SetName(ApproveButton, "Approve consent for hook " + selected.Id);
    }

    private string SummaryFor(HookViewState state)
    {
        var rows = _model?.Rows.Count ?? 0;
        return state switch
        {
            HookViewState.Success => rows + " hooks in run order. A hook runs only after consent for its exact command.",
            HookViewState.Degraded => rows + " hooks in run order; " + (_model?.DegradedCount ?? 0)
                + " degraded and will not run.",
            _ => string.Empty,
        };
    }

    private static string HeadingFor(HookViewState state) => state switch
    {
        HookViewState.Loading => "Loading hooks",
        HookViewState.Empty => "No hooks",
        HookViewState.Error => "Hooks could not be loaded",
        HookViewState.Success => "Hooks",
        HookViewState.Degraded => "Hooks need attention",
        _ => string.Empty,
    };

    // The accessible name of the action button that has focus, or null when focus is outside the rows and the consent panel.
    private string? FocusedActionName()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
        return focused is not null && IsInsideView(focused) ? AutomationProperties.GetName(focused) : null;
    }

    // The rows are rebuilt, which takes focus with them, and the new buttons are realized after this pass. So the focus is
    // given back on a later dispatcher turn, by name, and only if focus was lost in the meantime.
    private void RestoreFocusLater(string? name)
    {
        if (name is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => RestoreFocus(name), DispatcherPriority.Background);
    }

    private void RestoreFocus(string name)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control current && IsInsideView(current))
        {
            return;
        }

        var target = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));
        _ = target?.Focus();
    }

    private bool IsInsideView(Visual visual) =>
        ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this);
}
