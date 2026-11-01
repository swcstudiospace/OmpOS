using Autonogrammer.Presentation.Cowork;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>The Cowork composer: provider chips, attachment chips with remove, and send, retry and steer.</summary>
public partial class CoworkComposerView : UserControl
{
    private CoworkConversationModel? _model;

    /// <summary>Creates the view.</summary>
    public CoworkComposerView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as CoworkComposerModel);
    }

    /// <summary>Gets the send control.</summary>
    public Button Send => SendButton;

    /// <summary>Gets the retry control.</summary>
    public Button Retry => RetryButton;

    /// <summary>Gets the steer control.</summary>
    public Button Steer => SteerButton;

    private void Bind(CoworkComposerModel? surface)
    {
        _model = surface?.Conversation;
        ProviderChips.Children.Clear();
        AttachmentChips.Children.Clear();
        if (_model is null)
        {
            ImageToolChip.Text = string.Empty;
            return;
        }

        ImageToolChip.Text = _model.ImageToolMessage ?? ProviderStatusChip.AvailableLabel;
        AutomationProperties.SetName(
            ImageToolChip,
            _model.ImageToolsEnabled
                ? "Image tools, " + ProviderStatusChip.AvailableLabel
                : "Image tools, " + CoworkConversationModel.ModelCannotReadImages);
        foreach (var provider in _model.Providers)
        {
            var label = new TextBlock { Text = provider.Label };
            AutomationProperties.SetName(label, provider.AccessibleName);
            ProviderChips.Children.Add(label);
        }

        foreach (var chip in _model.Attachments)
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            var label = new TextBlock { Text = chip.Reason ?? chip.Name };
            AutomationProperties.SetName(label, chip.AccessibleName);
            row.Children.Add(label);
            var remove = new Button
            {
                Content = "Remove",
                Tag = chip.Id,
                MinHeight = 24,
                MinWidth = 24,
            };
            AutomationProperties.SetName(remove, chip.RemoveName);
            remove.Click += OnRemoveClick;
            row.Children.Add(remove);
            AttachmentChips.Children.Add(row);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            _model?.RemoveAttachment(id);
            Bind(_model is null ? null : _model.ComposerSurface);
        }
    }

#pragma warning disable VSTHRD100 // The click handler is the button boundary. The model awaits the host.
    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null)
        {
            await _model.TrySendAsync().ConfigureAwait(true);
        }
    }

    private async void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null)
        {
            await _model.RetryAsync().ConfigureAwait(true);
        }
    }

    private async void OnSteerClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null)
        {
            await _model.SteerAsync().ConfigureAwait(true);
        }
    }
#pragma warning restore VSTHRD100
}
