using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Presentation.Cloud;

namespace OmpOS.Native.Views.Cloud;

/// <summary>
/// The cost-confirmed hand-off dialog as a modal window over <see cref="CloudHandoffView"/>. It closes itself when the agent was started and when
/// the user cancels. The shell shows it with <c>await window.ShowDialog(owner)</c> and then reads <see cref="Result"/>.
/// </summary>
internal partial class CloudHandoffWindow : Window
{
    private readonly CloudHandoffModel _model;
    private readonly CloudHandoffView _view;

    /// <summary>Creates the window over a hand-off model.</summary>
    /// <param name="model">The model the window edits.</param>
    /// <exception cref="ArgumentNullException">The model is null.</exception>
    public CloudHandoffWindow(CloudHandoffModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();
        _model = model;
        _view = new CloudHandoffView { DataContext = model, Margin = new Avalonia.Thickness(16) };
        _model.Started += OnStarted;
        _model.CancelRequested += OnCancelRequested;
        Content = _view;
    }

    /// <summary>Gets the result of the hand-off when the window closed because the agent was started; null after Cancel, Esc or the close button.</summary>
    internal CloudHandoffResult? Result { get; private set; }

    /// <summary>Gets the content view.</summary>
    internal CloudHandoffView View => _view;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _model.Cancel();
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _view.FocusFirstField();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _model.Started -= OnStarted;
        _model.CancelRequested -= OnCancelRequested;
        base.OnClosed(e);
    }

    private void OnStarted(object? sender, CloudAgentCreatedEventArgs e)
    {
        Result = _model.Result;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e) => Close();
}
