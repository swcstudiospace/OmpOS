using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using OmpOS.Presentation.Chats;

namespace OmpOS.Native.Views.Chats;

/// <summary>
/// The side-chat region of a session: the Open side chat action, the list of side chats opened from the session, and the
/// messages of the selected one. It draws the bound <see cref="SideChatPanel"/> and forwards clicks and selections to it;
/// opening a side chat never touches the session's transcript.
/// </summary>
internal partial class SideChatListView : UserControl
{
    private SideChatPanel? _panel;
    private SideChat? _shown;
    private bool _syncing;

    /// <summary>Creates the side-chat region with no data context.</summary>
    public SideChatListView()
    {
        InitializeComponent();
        OpenSideChatButton.Click += OnOpenClick;
        SideChatRows.SelectionChanged += OnRowSelectionChanged;
    }

    /// <summary>Binds the region to the side-chat panel in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_panel is not null)
        {
            _panel.PropertyChanged -= OnPanelPropertyChanged;
        }

        _panel = DataContext as SideChatPanel;
        if (_panel is not null)
        {
            _panel.PropertyChanged += OnPanelPropertyChanged;
        }

        OpenSideChatButton.IsEnabled = _panel is not null;
        RenderChats();
        RenderSelection();
    }

    private void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _panel?.Open();

    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _panel is null)
        {
            return;
        }

        if (SideChatRows.SelectedItem is SideChat chat && !ReferenceEquals(chat, _panel.Selected))
        {
            _panel.Select(chat);
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUiThread(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(SideChatPanel.Chats):
                    RenderChats();
                    break;
                case nameof(SideChatPanel.Selected):
                    RenderSelection();
                    break;
            }
        });

    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUiThread(() =>
        {
            if (string.Equals(e.PropertyName, nameof(SideChat.Messages), StringComparison.Ordinal))
            {
                RenderMessages();
            }
        });

    private void RenderChats()
    {
        _syncing = true;
        try
        {
            SideChatRows.ItemsSource = _panel?.Chats;
            SideChatRows.SelectedItem = _panel?.Selected;
        }
        finally
        {
            _syncing = false;
        }

        EmptyText.IsVisible = _panel is null || _panel.Chats.Count == 0;
    }

    private void RenderSelection()
    {
        var selected = _panel?.Selected;
        if (!ReferenceEquals(selected, _shown))
        {
            if (_shown is not null)
            {
                _shown.PropertyChanged -= OnChatPropertyChanged;
            }

            _shown = selected;
            if (_shown is not null)
            {
                _shown.PropertyChanged += OnChatPropertyChanged;
            }
        }

        _syncing = true;
        try
        {
            SideChatRows.SelectedItem = selected;
        }
        finally
        {
            _syncing = false;
        }

        MessagesScroll.IsVisible = _shown is not null;
        RenderMessages();
    }

    private void RenderMessages()
    {
        MessageHost.Children.Clear();
        if (_shown is null)
        {
            return;
        }

        foreach (var message in _shown.Messages)
        {
            var speaker = message.Author == SideChatAuthor.User ? "You" : "Assistant";
            MessageHost.Children.Add(new TextBlock
            {
                Text = $"{speaker}: {message.Text}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
        }
    }

    private static void OnUiThread(Action action)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
