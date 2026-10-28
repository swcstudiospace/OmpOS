using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Settings window: a navigation list of the pages in model order, the selected page's view, a footer with Reset, Discard, Save and
/// Close, and the unsaved-changes prompt. It is not modal, and the head holds one instance of it. It draws the
/// <see cref="SettingsWindowModel"/> it is constructed with and holds no state of its own beyond focus.
/// </summary>
/// <remarks>
/// <para>
/// Closing goes through the model: Esc, Ctrl+W, the Close button and the window's own close box all call
/// <see cref="SettingsWindowModel.RequestClose"/>. With unsaved changes the window stays open and shows the prompt; the prompt takes the
/// focus and gives it back when it is dismissed. The window closes when the model reports <see cref="SettingsWindowModel.IsClosed"/>.
/// </para>
/// <para>
/// The window does not load the pages: the head calls <see cref="SettingsWindowModel.LoadAllAsync"/>, which fills every page, and a page
/// that failed to load shows its own Try again button.
/// </para>
/// </remarks>
internal partial class SettingsWindow : Window
{
    private readonly SettingsWindowModel _model;
    private readonly Dictionary<SettingsSection, ListBoxItem> _navItems = [];
    private readonly Dictionary<SettingsSection, TextBlock> _navMarkers = [];
    private readonly Dictionary<SettingsSection, Control> _views = [];
    private bool _syncing;
    private bool _closePosted;
    private string _status = string.Empty;
    private Control? _focusBeforePrompt;

    /// <summary>Creates the window over a model. The window subscribes to the model until it is closed.</summary>
    /// <param name="model">The window model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public SettingsWindow(SettingsWindowModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        _model = model;
        InitializeComponent();
        BuildNavigation();
        DataContext = model;

        model.PropertyChanged += OnModelChanged;
        foreach (var page in model.Pages)
        {
            page.PropertyChanged += OnPageChanged;
        }

        Navigation.SelectionChanged += OnNavigationChanged;
        Navigation.KeyDown += OnNavigationKeyDown;
        Opened += OnOpened;
        Closed += OnClosed;

        SelectInNavigation(model.SelectedSection);
        ShowSelectedPage();
        UpdateFooter();
    }

    /// <summary>Gets the navigation list.</summary>
    internal ListBox NavigationList => Navigation;

    /// <summary>Gets the navigation entry of a section.</summary>
    /// <param name="section">The section.</param>
    /// <returns>The entry, or null when the window has no such section.</returns>
    internal ListBoxItem? NavigationEntry(SettingsSection section) => _navItems.GetValueOrDefault(section);

    /// <summary>Gets the view of the selected page.</summary>
    internal Control? CurrentView => PageHost.Content as Control;

    /// <summary>Gets the Save button of the footer.</summary>
    internal Button Save => SaveButton;

    /// <summary>Gets the Reset button of the footer.</summary>
    internal Button Reset => ResetButton;

    /// <summary>Gets the Discard button of the footer.</summary>
    internal Button Discard => DiscardButton;

    /// <summary>Gets the Close button of the footer.</summary>
    internal Button CloseWindow => CloseButton;

    /// <summary>Gets the unsaved-changes prompt.</summary>
    internal Border Prompt => ClosePrompt;

    /// <summary>Gets the prompt button that saves and closes.</summary>
    internal Button PromptSave => PromptSaveButton;

    /// <summary>Gets the prompt button that discards and closes.</summary>
    internal Button PromptDiscard => PromptDiscardButton;

    /// <summary>Gets the prompt button that keeps the window open.</summary>
    internal Button PromptKeep => PromptKeepButton;

    /// <summary>Gets the polite live region of the footer.</summary>
    internal TextBlock Footer => FooterLine;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        // A control that handled Esc (a text entry that closes its own popup, a key entry that cancels) never gets here.
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            if (_model.IsClosePromptVisible)
            {
                _ = ResolvePromptAsync(SettingsClosePromptChoice.KeepEditing);
            }
            else
            {
                _ = _model.RequestClose();
            }
        }
        else if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            _ = _model.RequestClose();
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnClosing(e);
        if (_model.IsClosed || e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }

        // The close box asks like Esc does: the model closes the window, or opens the prompt when something is unsaved.
        e.Cancel = true;
        _ = _model.RequestClose();
    }

    private void BuildNavigation()
    {
        foreach (var page in _model.Pages)
        {
            var title = new TextBlock { Text = page.Title, VerticalAlignment = VerticalAlignment.Center };
            var marker = new TextBlock { Text = SettingsViewCopy.DirtyMarker, IsVisible = page.IsDirty };
            marker.Classes.Add("settings-dirty-marker");
            AutomationProperties.SetAccessibilityView(marker, AccessibilityView.Raw);

            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(title, 0);
            Grid.SetColumn(marker, 1);
            content.Children.Add(title);
            content.Children.Add(marker);

            var item = new ListBoxItem { Content = content, Tag = page };
            AutomationProperties.SetName(item, SettingsCopy.NavigationEntryName(page.Title, page.IsDirty));
            Navigation.Items.Add(item);
            _navItems[page.Section] = item;
            _navMarkers[page.Section] = marker;
        }
    }

    private void SelectInNavigation(SettingsSection section)
    {
        _syncing = true;
        try
        {
            Navigation.SelectedItem = _navItems.GetValueOrDefault(section);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ShowSelectedPage()
    {
        var page = _model.SelectedPage;
        if (!_views.TryGetValue(page.Section, out var view))
        {
            view = SettingsPageViews.Create(page);
            _views[page.Section] = view;
        }

        PageHost.Content = view;
        _status = string.Empty;
    }

    private void UpdateFooter()
    {
        var page = _model.SelectedPage;
        var editable = SettingsPageViews.IsEditable(page);
        var ready = SettingsPageViews.IsReady(page);

        SaveButton.IsVisible = editable;
        DiscardButton.IsVisible = editable;
        ResetButton.IsVisible = SettingsPageViews.HasDefaults(page);
        SaveButton.IsEnabled = ready && page.IsDirty && !page.HasError;
        DiscardButton.IsEnabled = page.IsDirty;
        ResetButton.IsEnabled = ready;
        FooterLine.Text = page.IsDirty ? SettingsCopy.UnsavedBadge : _status;
    }

    private void UpdateNavigationEntry(ISettingsPage page)
    {
        if (_navItems.TryGetValue(page.Section, out var item))
        {
            AutomationProperties.SetName(item, SettingsCopy.NavigationEntryName(page.Title, page.IsDirty));
            _navMarkers[page.Section].IsVisible = page.IsDirty;
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (string.IsNullOrEmpty(name) || string.Equals(name, nameof(SettingsWindowModel.SelectedSection), StringComparison.Ordinal))
        {
            SelectInNavigation(_model.SelectedSection);
            ShowSelectedPage();
            UpdateFooter();
        }

        if (string.IsNullOrEmpty(name) || string.Equals(name, nameof(SettingsWindowModel.IsClosePromptVisible), StringComparison.Ordinal))
        {
            UpdatePrompt();
        }

        if (string.IsNullOrEmpty(name) || string.Equals(name, nameof(SettingsWindowModel.HasUnsavedChanges), StringComparison.Ordinal))
        {
            UpdateFooter();
        }

        if (_model.IsClosed && !_closePosted)
        {
            // Closing from inside a property notification of a close that is already running would re-enter the window's own close.
            _closePosted = true;
            Dispatcher.UIThread.Post(() => Close(), DispatcherPriority.Normal);
        }
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ISettingsPage page)
        {
            return;
        }

        UpdateNavigationEntry(page);
        if (ReferenceEquals(page, _model.SelectedPage))
        {
            UpdateFooter();
        }
    }

    private void UpdatePrompt()
    {
        if (_model.IsClosePromptVisible)
        {
            _focusBeforePrompt = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            PromptSaveButton.FocusWhenReady();
        }
        else if (!_model.IsClosed)
        {
            (_focusBeforePrompt ?? Navigation).FocusWhenReady();
            _focusBeforePrompt = null;
        }
    }

    private void OnNavigationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && Navigation.SelectedItem is ListBoxItem { Tag: ISettingsPage page })
        {
            _model.SelectSection(page.Section);
        }
    }

    private void OnNavigationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || Navigation.Items.Count == 0)
        {
            return;
        }

        var target = e.Key switch
        {
            Key.Home => Navigation.Items[0],
            Key.End => Navigation.Items[^1],
            _ => null,
        };
        if (target is ListBoxItem item)
        {
            e.Handled = true;
            Navigation.SelectedItem = item;
            item.Focus(NavigationMethod.Directional);
        }
    }

    private void OnOpened(object? sender, EventArgs e) => _navItems.GetValueOrDefault(_model.SelectedSection).FocusWhenReady();

    private void OnClosed(object? sender, EventArgs e)
    {
        _model.PropertyChanged -= OnModelChanged;
        foreach (var page in _model.Pages)
        {
            page.PropertyChanged -= OnPageChanged;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveSelectedAsync();

    private async Task SaveSelectedAsync()
    {
        var saved = await _model.SaveAsync(_model.SelectedSection, CancellationToken.None).ConfigureAwait(true);
        _status = saved ? string.Empty : SettingsCopy.SaveFailedHeading;
        UpdateFooter();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _status = string.Empty;
        _model.ResetSection(_model.SelectedSection);
        UpdateFooter();
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        _status = string.Empty;
        _model.SelectedPage.Discard();
        UpdateFooter();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _ = _model.RequestClose();

    private void OnPromptSaveClick(object? sender, RoutedEventArgs e) => _ = ResolvePromptAsync(SettingsClosePromptChoice.Save);

    private void OnPromptDiscardClick(object? sender, RoutedEventArgs e) => _ = ResolvePromptAsync(SettingsClosePromptChoice.Discard);

    private void OnPromptKeepClick(object? sender, RoutedEventArgs e) => _ = ResolvePromptAsync(SettingsClosePromptChoice.KeepEditing);

    private async Task ResolvePromptAsync(SettingsClosePromptChoice choice)
    {
        if (!_model.IsClosePromptVisible)
        {
            return;
        }

        var closed = await _model.ResolveClosePromptAsync(choice, CancellationToken.None).ConfigureAwait(true);
        if (!closed && choice == SettingsClosePromptChoice.Save)
        {
            // The page that refused its save is selected again and shows its own inline errors.
            _status = SettingsCopy.SaveFailedHeading;
            UpdateFooter();
        }
    }
}
