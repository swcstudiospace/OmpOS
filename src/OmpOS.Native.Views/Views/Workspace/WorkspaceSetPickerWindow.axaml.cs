using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Views.Workspace;

/// <summary>
/// The workspace set picker as a dialog window over <see cref="WorkspaceSetPickerView"/>. Esc and Done close it without starting; Start sets
/// <see cref="Started"/>, raises <see cref="StartRequested"/> and closes it, and the shell then reads <see cref="WorkspaceSetPicker.Snapshot"/> from <see cref="Picker"/>.
/// The shell shows it with <c>ShowDialog</c>.
/// </summary>
internal partial class WorkspaceSetPickerWindow : Window
{
    private readonly WorkspaceSetPickerView _view;

    /// <summary>Creates the window over a picker model.</summary>
    /// <param name="picker">The picker model the window edits.</param>
    /// <param name="dialogs">The folder and file dialogs, or null for the platform dialogs over this window.</param>
    /// <exception cref="ArgumentNullException">The picker is null.</exception>
    public WorkspaceSetPickerWindow(WorkspaceSetPicker picker, IWorkspaceSetPickerDialogs? dialogs = null)
    {
        ArgumentNullException.ThrowIfNull(picker);

        InitializeComponent();
        Picker = picker;
        _view = new WorkspaceSetPickerView { DataContext = picker };
        if (dialogs is not null)
        {
            _view.Dialogs = dialogs;
        }

        _view.StartRequested += OnViewStartRequested;
        _view.CancelRequested += OnViewCancelRequested;
        Content = _view;
    }

    /// <summary>Raised when the user starts a session over the set, just before the window closes.</summary>
    internal event EventHandler? StartRequested;

    /// <summary>Gets the picker model the window edits.</summary>
    internal WorkspaceSetPicker Picker { get; }

    /// <summary>Gets whether the window closed because the user pressed Start. False after Esc, Done or the close button.</summary>
    internal bool Started { get; private set; }

    /// <summary>Gets the content view.</summary>
    internal WorkspaceSetPickerView View => _view;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Close();
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = _view.NameInput.Focus();
    }

    private void OnViewStartRequested(object? sender, EventArgs e)
    {
        Started = true;
        StartRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnViewCancelRequested(object? sender, EventArgs e) => Close();
}
