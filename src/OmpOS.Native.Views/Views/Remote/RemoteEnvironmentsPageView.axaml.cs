using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Remote;

namespace OmpOS.Native.Views.Remote;

/// <summary>
/// The Remote environments page: the saved ssh environments with their status in words and their findings, the hosts of the ssh config that can be added,
/// and the add or edit form. The view loads the model once when it gets one and never starts a connection by itself: a host is contacted only from the
/// Test connection button of its row. Focus moves into the form when it opens and back to the Add button when it closes.
/// </summary>
internal partial class RemoteEnvironmentsPageView : UserControl
{
    private RemoteEnvironmentsViewModel? _model;
    private EventHandler? _pendingFocus;

    /// <summary>Creates a view with no model.</summary>
    public RemoteEnvironmentsPageView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the list of saved environments.</summary>
    internal ItemsControl Environments => EnvironmentRows;

    /// <summary>Gets the list of ssh config hosts that can be added.</summary>
    internal ItemsControl Candidates => CandidateRows;

    /// <summary>Gets the button that starts a hand-entered environment.</summary>
    internal Button AddManual => AddManualButton;

    /// <summary>Gets the Try again button of the failed-load state.</summary>
    internal Button Retry => RetryButton;

    /// <summary>Gets the polite live region.</summary>
    internal TextBlock Announcement => AnnouncementText;

    /// <summary>Gets the text of the empty state.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the text of the loading state.</summary>
    internal TextBlock Loading => LoadingText;

    /// <summary>Gets the card that holds the add or edit form.</summary>
    internal Border EditorFrame => EditorCard;

    /// <summary>Gets the section of the container and WSL environments, or null when the head has none.</summary>
    internal RuntimeEnvironmentsView? RuntimeSection => RuntimeHost.Content as RuntimeEnvironmentsView;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext switch
        {
            RemoteEnvironmentsViewModel direct => direct,
            IRemoteEnvironmentsModelSource holder => holder.Model,
            _ => null,
        };

        // Compiled bindings read the model, whatever object holds it as the view's data context. When the view's own data context is the model, the page
        // inherits it: assigning the same value locally from inside this change notification leaves the bindings of the page unattached.
        if (ReferenceEquals(DataContext, _model))
        {
            PageScroll.ClearValue(DataContextProperty);
        }
        else
        {
            PageScroll.DataContext = _model;
        }

        RuntimeHost.Content = DataContext is OmpOS.Presentation.Settings.RemoteEnvironmentsSettingsPage { Runtime: { } runtime }
            ? new RuntimeEnvironmentsView { DataContext = runtime }
            : null;

        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;

            // LoadAsync reports its failures in the model's state and never throws them, so the task is not observed here.
            _ = _model.LoadAsync(CancellationToken.None);
        }
    }

    private static T? Find<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().FirstOrDefault(control => string.Equals(control.Name, name, StringComparison.Ordinal));

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(RemoteEnvironmentsViewModel.Editor), StringComparison.Ordinal))
        {
            return;
        }

        // The form is built by a template, so its first field exists only after a layout pass: focus is requested again after each pass until the field is there.
        CancelPendingFocus();
        if (_model?.Editor is not null)
        {
            FocusEditorStart();
        }
        else
        {
            Dispatcher.UIThread.Post(() => AddManualButton.FocusWhenReady(), DispatcherPriority.Input);
        }
    }

    private bool TryFocusEditorStart()
    {
        var first = Find<TextBox>(this, "DisplayNameBox");
        if (first is null || !first.IsEffectivelyVisible)
        {
            return false;
        }

        return first.Focus();
    }

    private void FocusEditorStart()
    {
        _pendingFocus = (_, _) =>
        {
            if (TryFocusEditorStart())
            {
                CancelPendingFocus();
            }
        };
        LayoutUpdated += _pendingFocus;
        Dispatcher.UIThread.Post(() => _pendingFocus?.Invoke(this, EventArgs.Empty), DispatcherPriority.Input);
    }

    private void CancelPendingFocus()
    {
        if (_pendingFocus is not null)
        {
            LayoutUpdated -= _pendingFocus;
            _pendingFocus = null;
        }
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => _ = _model?.LoadAsync(CancellationToken.None);

    private void OnAddManualClick(object? sender, RoutedEventArgs e) => _model?.StartAdd(alias: null);

    private void OnAddCandidateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: string alias })
        {
            _model?.StartAdd(alias);
        }
    }

    private void OnTestClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RemoteProfileRow row } && _model is not null)
        {
            _ = _model.TestConnectionAsync(row, CancellationToken.None);
        }
    }

    private void OnCancelTestClick(object? sender, RoutedEventArgs e) => _model?.CancelTest();

    private void OnTrustHostClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RemoteProfileRow row } && _model is not null)
        {
            _ = _model.TrustHostAsync(row, CancellationToken.None);
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RemoteProfileRow row })
        {
            _model?.StartEdit(row);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: RemoteProfileRow row } && _model is not null)
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
