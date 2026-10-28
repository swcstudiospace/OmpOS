using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.Attachments;
using Autonogrammer.Presentation.Composer.Attachments;

namespace Autonogrammer.Native.Views;

/// <summary>Image input of the composer: the Attach button, paste, drag and drop, and the preview strip. The rules live in <see cref="ComposerAttachmentsModel"/>.</summary>
internal partial class ComposerView
{
    private ComposerAttachmentsModel? _imageModel;

    /// <summary>Gets or sets the file picker behind the Attach button. Without one the button stays hidden.</summary>
    internal IFilePicker? FilePicker
    {
        get;
        set
        {
            field = value;
            RefreshAttachments();
        }
    }

    /// <summary>Gets or sets the clipboard reader that Ctrl+V consults for an image. Without one only text pastes.</summary>
    internal IClipboardImageSource? ClipboardImages { get; set; }

    /// <summary>Gets the Attach button of the footer.</summary>
    internal Button AttachButtonControl => AttachButton;

    /// <summary>Gets the list of attached image previews.</summary>
    internal ItemsControl ImageStripList => ImageStrip;

    /// <summary>Gets the notice under the previews.</summary>
    internal TextBlock ImageNoticeText => ImageNotice;

    /// <summary>Gets the panel that holds the previews and the notice.</summary>
    internal StackPanel ImageStripHost => ImageStripPanel;

    private void InitialiseAttachments()
    {
        AttachButton.Click += OnAttachClick;
        PromptInput.AddHandler(KeyDownEvent, OnPastePressed, RoutingStrategies.Tunnel);
        ImageStrip.AddHandler(Button.ClickEvent, OnImageStripClick);
        ImageStrip.AddHandler(KeyDownEvent, OnImageStripKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(ComposerColumn, true);
        ComposerColumn.AddHandler(DragDrop.DragOverEvent, OnImageDragOver);
        ComposerColumn.AddHandler(DragDrop.DropEvent, OnImageDrop);
    }

    private void BindAttachments()
    {
        var images = _composer?.Images;
        if (!ReferenceEquals(_imageModel, images))
        {
            if (_imageModel is not null)
            {
                _imageModel.PropertyChanged -= OnImageModelChanged;
            }

            _imageModel = images;
            if (_imageModel is not null)
            {
                _imageModel.PropertyChanged += OnImageModelChanged;
            }
        }

        RefreshAttachments();
    }

    private void OnImageModelChanged(object? sender, PropertyChangedEventArgs e) => RefreshAttachments();

    private void RefreshAttachments()
    {
        var model = _imageModel;
        AttachButton.IsVisible = model is not null && FilePicker is not null;
        AttachButton.IsEnabled = model is { CanAttach: true };
        var help = model is { CanAttach: false } ? model.DisabledReason : string.Empty;
        AutomationProperties.SetHelpText(AttachButton, help);
        ToolTip.SetTip(AttachButton, help.Length > 0 ? AttachmentCopy.AttachName + ". " + help : AttachmentCopy.AttachName);

        ImageStrip.ItemsSource = model?.Items;
        var notice = model switch
        {
            null => string.Empty,
            { Notice.Length: > 0 } => model.Notice,
            { CanAttach: false } => model.DisabledReason,
            { HasItems: true } => model.CapabilityNote,
            _ => string.Empty,
        };
        ImageNotice.Text = notice;
        ImageNotice.IsVisible = notice.Length > 0;
        ImageStripPanel.IsVisible = model is not null && (model.HasItems || notice.Length > 0);
    }

    /// <summary>Adds images read from a picker, the clipboard or a drop; every refusal reaches the notice of the model.</summary>
    /// <param name="images">The images.</param>
    /// <returns>The number attached.</returns>
    internal int AddImages(IEnumerable<PickedImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        var added = 0;
        if (_imageModel is not { } model)
        {
            return 0;
        }

        foreach (var image in images)
        {
            if (model.Add(image.Data, image.MimeType, image.Name).Succeeded)
            {
                added++;
            }
        }

        return added;
    }

    private void OnAttachClick(object? sender, RoutedEventArgs e)
    {
        if (FilePicker is { } picker && _imageModel is { CanAttach: true } model && _composer is { } composer)
        {
            Track(composer, async () =>
            {
                var picked = await picker.PickImagesAsync(model.Limits.MaxBytes, CancellationToken.None).ConfigureAwait(true);
                AddImages(picked);
                FocusPrompt();
            });
        }
    }

    private void OnPastePressed(object? sender, KeyEventArgs e)
    {
        var paste = (e.Key == Key.V && (e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.None)
            || (e.Key == Key.Insert && (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None);
        if (!paste || ClipboardImages is not { } source || _imageModel is not { CanAttach: true } || _composer is not { } composer)
        {
            return;
        }

        // Not handled: the text box still pastes any text. An image on the clipboard is added when the read answers.
        Track(composer, async () =>
        {
            var image = await source.TryGetImageAsync(CancellationToken.None).ConfigureAwait(true);
            if (image is not null)
            {
                AddImages([image]);
            }
        });
    }

    private void OnImageStripClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Control source
            && source.FindAncestorOfType<Button>(includeSelf: true) is { DataContext: ComposerAttachment attachment } button
            && button.Classes.Contains("attachment-remove"))
        {
            RemoveImage(attachment);
        }
    }

    private void OnImageStripKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete
            && e.Source is Control source
            && source.FindAncestorOfType<Button>(includeSelf: true) is { DataContext: ComposerAttachment attachment })
        {
            e.Handled = true;
            RemoveImage(attachment);
        }
    }

    private void RemoveImage(ComposerAttachment attachment)
    {
        if (_imageModel?.Remove(attachment) == true)
        {
            FocusPrompt();
        }
    }

    private void OnImageDragOver(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is { Length: > 0 })
        {
            e.DragEffects = _imageModel is { CanAttach: true } ? DragDropEffects.Copy : DragDropEffects.None;
        }
    }

    private void OnImageDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: > 0 } || _imageModel is not { } model || _composer is not { } composer)
        {
            return;
        }

        e.Handled = true;
        if (!model.CanAttach)
        {
            return;
        }

        var dropped = files.OfType<IStorageFile>().ToList();
        Track(composer, async () =>
        {
            var read = new List<PickedImage>(dropped.Count);
            foreach (var file in dropped)
            {
                read.Add(await StorageImageReader.ReadAsync(file, model.Limits.MaxBytes, CancellationToken.None).ConfigureAwait(true));
            }

            AddImages(read);
        });
    }
}
