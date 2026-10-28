using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Remote;

namespace OmpOS.Native.Views.Remote;

/// <summary>
/// The environment selector of the New session dialog. A choice in the list goes to the <see cref="NewSessionTargetModel"/>; the message under the
/// list is its validation. The Manage button only raises <see cref="ManageRequested"/>: the dialog decides where the settings page opens.
/// </summary>
internal partial class EnvironmentPickerView : UserControl
{
    private NewSessionTargetModel? _model;

    /// <summary>Creates a view with no model.</summary>
    public EnvironmentPickerView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user presses Manage remote environments.</summary>
    internal event EventHandler? ManageRequested;

    /// <summary>Gets the list of run locations.</summary>
    internal ListBox Targets => TargetList;

    /// <summary>Gets the line that says why the choice cannot start, or that it is ready.</summary>
    internal TextBlock ValidationMessage => ValidationText;

    /// <summary>Gets the Manage remote environments button.</summary>
    internal Button Manage => ManageButton;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _model = DataContext as NewSessionTargetModel;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_model is not null && TargetList.SelectedItem is NewSessionTargetOption option && !ReferenceEquals(option, _model.Selected))
        {
            _model.Select(option.Id);
        }
    }

    private void OnManageClick(object? sender, RoutedEventArgs e) => ManageRequested?.Invoke(this, EventArgs.Empty);
}
