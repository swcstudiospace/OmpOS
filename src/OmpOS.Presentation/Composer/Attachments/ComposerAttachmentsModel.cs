using System.Globalization;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Composer.Attachments;

/// <summary>
/// The images attached to the message being composed. Bytes are validated by their leading bytes, the count and size limits hold, and
/// the <see cref="AttachmentGate"/> decides whether images are accepted at all. Names are shown but never logged.
/// </summary>
public sealed class ComposerAttachmentsModel : ObservableModel
{
    private const int ThumbnailEdge = 64;
    private const int MaxNameLength = 80;

    private readonly AttachmentLimits _limits;
    private readonly IImageThumbnailer? _thumbnailer;
    private IReadOnlyList<ComposerAttachment> _items = [];
    private AttachmentGateDecision _gate = AttachmentGate.Evaluate(null, VisionSupport.Unknown);
    private string _notice = string.Empty;
    private long _totalBytes;
    private int _nextId;

    /// <summary>Creates an empty model.</summary>
    /// <param name="thumbnailer">The preview maker, or <see langword="null"/> for no previews.</param>
    /// <param name="limits">The limits, or <see langword="null"/> for the defaults.</param>
    public ComposerAttachmentsModel(IImageThumbnailer? thumbnailer = null, AttachmentLimits? limits = null)
    {
        _thumbnailer = thumbnailer;
        _limits = limits ?? new AttachmentLimits();
    }

    /// <summary>Gets the attached images in order. The list is replaced on every change.</summary>
    public IReadOnlyList<ComposerAttachment> Items
    {
        get => _items;
        private set
        {
            _items = value;
            _totalBytes = value.Sum(item => (long)item.Data.Length);
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(HasItems));
            NotifyPropertyChanged(nameof(TotalBytes));
            NotifyPropertyChanged(nameof(TotalSizeText));
        }
    }

    /// <summary>Gets whether any image is attached.</summary>
    public bool HasItems => _items.Count > 0;

    /// <summary>Gets the sum of the image sizes in bytes.</summary>
    public long TotalBytes => _totalBytes;

    /// <summary>Gets the sum of the image sizes as text.</summary>
    public string TotalSizeText => AttachmentCopy.FormatSize(_totalBytes);

    /// <summary>Gets the limits.</summary>
    public AttachmentLimits Limits => _limits;

    /// <summary>Gets the latest message about a refused image or a caution, or an empty string.</summary>
    public string Notice
    {
        get => _notice;
        private set => SetProperty(ref _notice, value);
    }

    /// <summary>Gets whether images may be attached.</summary>
    public bool CanAttach => _gate.CanAttach;

    /// <summary>Gets why attaching is disabled, or an empty string.</summary>
    public string DisabledReason => _gate.Reason;

    /// <summary>Gets the caution shown while attaching is allowed, or an empty string.</summary>
    public string CapabilityNote => _gate.Note;

    /// <summary>Sets what the engine and the selected model accept; call it again when the model changes.</summary>
    /// <param name="promptImageCapability">The engine's <c>promptCapabilities.image</c>, or <see langword="null"/> when unknown.</param>
    /// <param name="vision">Whether the selected model reads images.</param>
    public void SetCapabilities(bool? promptImageCapability, VisionSupport vision)
    {
        _gate = AttachmentGate.Evaluate(promptImageCapability, vision);
        NotifyPropertyChanged(nameof(CanAttach));
        NotifyPropertyChanged(nameof(DisabledReason));
        NotifyPropertyChanged(nameof(CapabilityNote));
    }

    /// <summary>Adds one image.</summary>
    /// <param name="data">The encoded image.</param>
    /// <param name="mimeHint">The type the source declared, advisory only; the bytes decide. A non-image type is refused.</param>
    /// <param name="name">The file name; any path is dropped.</param>
    /// <returns>The attachment or the reason it was refused. A refusal also sets <see cref="Notice"/>.</returns>
    public AttachmentAddResult Add(byte[] data, string? mimeHint, string? name)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!_gate.CanAttach)
        {
            return Refuse(AttachmentError.Refused, _gate.Reason);
        }

        if (_items.Count >= _limits.MaxCount)
        {
            return Refuse(AttachmentError.TooMany, AttachmentCopy.TooMany(_limits.MaxCount));
        }

        if (data.Length == 0)
        {
            return Refuse(AttachmentError.Empty, AttachmentCopy.Empty);
        }

        if (data.Length > _limits.MaxBytes)
        {
            return Refuse(AttachmentError.TooLarge, AttachmentCopy.TooLarge(_limits.MaxBytes));
        }

        if (_totalBytes + data.Length > _limits.MaxTotalBytes)
        {
            return Refuse(AttachmentError.TotalTooLarge, AttachmentCopy.TotalTooLarge(_limits.MaxTotalBytes));
        }

        var mime = ImageSniffer.Detect(data);
        var hintIsNonImage = !string.IsNullOrWhiteSpace(mimeHint) && !mimeHint.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        if (mime is null || hintIsNonImage)
        {
            return Refuse(AttachmentError.UnsupportedType, AttachmentCopy.Unsupported);
        }

        var display = DisplayName(name, mime);
        var id = string.Create(CultureInfo.InvariantCulture, $"image-{++_nextId}");
        var thumbnail = _thumbnailer?.CreateThumbnail(data, mime, ThumbnailEdge);
        var attachment = new ComposerAttachment(id, display, mime, data, thumbnail, AttachmentCopy.FormatSize(data.Length));
        Items = [.. _items, attachment];
        Notice = _gate.Note;
        return new AttachmentAddResult(attachment, AttachmentError.None, string.Empty);
    }

    /// <summary>Removes an image.</summary>
    /// <param name="attachment">The attachment to remove.</param>
    /// <returns><see langword="true"/> when it was attached.</returns>
    public bool Remove(ComposerAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var kept = _items.Where(item => !string.Equals(item.Id, attachment.Id, StringComparison.Ordinal)).ToList();
        if (kept.Count == _items.Count)
        {
            return false;
        }

        Items = kept;
        Notice = string.Empty;
        return true;
    }

    /// <summary>Removes every image, after a send.</summary>
    public void Clear()
    {
        if (_items.Count > 0)
        {
            Items = [];
        }

        Notice = string.Empty;
    }

    private AttachmentAddResult Refuse(AttachmentError error, string message)
    {
        Notice = message;
        return new AttachmentAddResult(null, error, message);
    }

    private static string DisplayName(string? name, string mime)
    {
        var fallback = "image." + mime["image/".Length..];
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var cut = name.LastIndexOfAny(['/', '\\']);
        var leaf = cut >= 0 ? name[(cut + 1)..] : name;
        var clean = new string([.. leaf.Where(c => !char.IsControl(c))]).Trim();
        if (clean.Length == 0)
        {
            return fallback;
        }

        return clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;
    }
}
