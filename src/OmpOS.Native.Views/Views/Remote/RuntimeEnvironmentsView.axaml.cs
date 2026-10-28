using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Presentation.Remote;

namespace OmpOS.Native.Views.Remote;

/// <summary>
/// The Containers and WSL section of the Remote page. It draws a <see cref="RuntimeEnvironmentsViewModel"/>: the saved container and WSL environments with
/// their status and findings, the running containers podman lists, and the add or edit form. It reads the saved environments when it gets its model and
/// contacts podman or wsl.exe only when the user presses a button.
/// </summary>
internal partial class RuntimeEnvironmentsView : UserControl
{
    private RuntimeEnvironmentsViewModel? _model;

    /// <summary>Initializes a new instance of the <see cref="RuntimeEnvironmentsView"/> class.</summary>
    public RuntimeEnvironmentsView()
    {
        InitializeComponent();
        PickerList.ContainerPrepared += OnPickerItemPrepared;
    }

    /// <summary>Gets the list of saved environments.</summary>
    internal ItemsControl Environments => EnvironmentRows;

    /// <summary>Gets the button that opens the list of running containers.</summary>
    internal Button AddFromRunning => AddFromRunningButton;

    /// <summary>Gets the button that opens the form for a container entered by name.</summary>
    internal Button AddContainer => AddContainerButton;

    /// <summary>Gets the button that opens the form for a WSL distribution. It is hidden where WSL is not available.</summary>
    internal Button AddWsl => AddWslButton;

    /// <summary>Gets the note shown where WSL is not available.</summary>
    internal TextBlock WslNote => WslNoteText;

    /// <summary>Gets the frame of the list of running containers.</summary>
    internal Border Picker => PickerCard;

    /// <summary>Gets the list of running containers.</summary>
    internal ListBox PickerItems => PickerList;

    /// <summary>Gets the button that adds the chosen running container.</summary>
    internal Button PickerAdd => PickerAddButton;

    /// <summary>Gets the frame of the add or edit form.</summary>
    internal Border EditorFrame => EditorCard;

    /// <summary>Gets the save button of the form.</summary>
    internal Button Save => SaveButton;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Announcement => AnnouncementText;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _model = DataContext as RuntimeEnvironmentsViewModel;
        if (_model is not null)
        {
            // LoadAsync reports its failures in the model's state and never throws them, so the task is not observed here.
            _ = _model.LoadAsync(CancellationToken.None);
        }
    }

    // The item's own name (container, image and the dev container note) is the accessible name of its list entry, which is the tab stop.
    private static void OnPickerItemPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (sender is ListBox list && e.Index >= 0 && e.Index < list.Items.Count && list.Items[e.Index] is RunningContainerItem item)
        {
            Avalonia.Automation.AutomationProperties.SetName(e.Container, item.AccessibleName);
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = _model?.LoadAsync(CancellationToken.None);

    private void OnAddFromRunningClick(object? sender, RoutedEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        PickerCard.IsVisible = true;

        // The listing reports its failures in the picker's state and never throws them.
        _ = _model.OpenPickerAsync(CancellationToken.None);
    }

    private void OnPickerAddClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && _model.StartAddFromPicker())
        {
            PickerCard.IsVisible = false;
        }
    }

    private void OnPickerCloseClick(object? sender, RoutedEventArgs e) => PickerCard.IsVisible = false;

    private void OnAddContainerClick(object? sender, RoutedEventArgs e)
    {
        PickerCard.IsVisible = false;
        _model?.StartAddContainer();
    }

    private void OnAddWslClick(object? sender, RoutedEventArgs e)
    {
        PickerCard.IsVisible = false;
        _ = _model?.StartAddWsl();
    }

    private void OnTestClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RuntimeEnvironmentRow row } && _model is not null)
        {
            _ = _model.TestAsync(row, CancellationToken.None);
        }
    }

    private void OnCancelRunClick(object? sender, RoutedEventArgs e) => _model?.CancelRun();

    private void OnStartContainerClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RuntimeEnvironmentRow row } && _model is not null)
        {
            _ = _model.StartContainerAsync(row, CancellationToken.None);
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RuntimeEnvironmentRow row })
        {
            _model?.StartEdit(row);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RuntimeEnvironmentRow row } && _model is not null)
        {
            _ = _model.DeleteAsync(row, CancellationToken.None);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null)
        {
            _ = _model.SaveEditAsync(CancellationToken.None);
        }
    }

    private void OnCancelEditClick(object? sender, RoutedEventArgs e) => _model?.CancelEdit();
}
