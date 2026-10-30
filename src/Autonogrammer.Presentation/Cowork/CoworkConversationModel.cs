using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Composer.Attachments;
using Autonogrammer.Presentation.Observable;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The Cowork conversation: the existing session and composer models, the approval queue, provider chips and
/// attachment chips. Image limits match <c>AcpImageLimits</c> (8 images, 10 MB each, 25 MB total) without a Harness reference.
/// </summary>
public sealed class CoworkConversationModel : ObservableModel
{
    /// <summary>The sentence shown when the selected model reads no images. It is the only refusal copy a Cowork session surfaces.</summary>
    public const string ModelCannotReadImages = "This model cannot read images";

    /// <summary>The sentence shown when an attachment's file is missing.</summary>
    public const string MissingFile = "The file is missing.";

    /// <summary>The accessible name of Send.</summary>
    public const string SendName = "Send";

    /// <summary>The accessible name of Retry.</summary>
    public const string RetryName = "Retry";

    /// <summary>The accessible name of Steer.</summary>
    public const string SteerName = "Steer";

    /// <summary>The accessible name of the transcript region.</summary>
    public const string TranscriptName = "Transcript";

    /// <summary>The accessible name of the composer region.</summary>
    public const string ComposerName = "Composer";

    /// <summary>The accessible name of the approvals region.</summary>
    public const string ApprovalsName = "Approvals";

    private readonly List<CoworkAttachmentChip> _attachments = [];
    private readonly Func<string, Task>? _onSteer;
    private readonly Func<Task>? _onRetry;
    private bool _readsImages;

    /// <summary>Creates a conversation over the session and composer the host already built.</summary>
    /// <param name="session">The transcript model.</param>
    /// <param name="composer">The composer model.</param>
    /// <param name="approvals">The approval queue. Null starts an empty queue.</param>
    /// <param name="providers">Provider chips. Null starts with none.</param>
    /// <param name="readsImages">Whether the selected model reads images. Change <see cref="ReadsImages"/> later to change it in place.</param>
    /// <param name="onSteer">Delivers a steering draft into the running turn. Null when the host has no steer channel.</param>
    /// <param name="onRetry">Retries a recoverable failure. Null when the host has no retry channel.</param>
    /// <param name="motion">The motion setting the views honour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> or <paramref name="composer"/> is null.</exception>
    public CoworkConversationModel(
        SessionViewModel session,
        ComposerViewModel composer,
        CoworkApprovalQueue? approvals = null,
        IReadOnlyList<ProviderStatusChip>? providers = null,
        bool readsImages = true,
        Func<string, Task>? onSteer = null,
        Func<Task>? onRetry = null,
        MotionPreference motion = MotionPreference.System)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(composer);
        Session = session;
        Composer = composer;
        Approvals = approvals ?? new CoworkApprovalQueue();
        Providers = providers ?? [];
        _readsImages = readsImages;
        _onSteer = onSteer;
        _onRetry = onRetry;
        Motion = motion;
        ComposerSurface = new CoworkComposerModel(this);
    }

    /// <summary>Gets the image limits. They equal the ACP image limits: 8 images, 10 MB each, 25 MB total.</summary>
    public static AttachmentLimits ImageLimits { get; } = new();

    /// <summary>Gets the transcript model.</summary>
    public SessionViewModel Session { get; }

    /// <summary>Gets the composer model.</summary>
    public ComposerViewModel Composer { get; }

    /// <summary>Gets the approval queue.</summary>
    public CoworkApprovalQueue Approvals { get; }

    /// <summary>Gets the provider chips.</summary>
    public IReadOnlyList<ProviderStatusChip> Providers { get; }

    /// <summary>Gets the composer surface <c>ViewMapping</c> binds to the composer view.</summary>
    public CoworkComposerModel ComposerSurface { get; }

    /// <summary>Gets the attachment chips, in the order they were added.</summary>
    public IReadOnlyList<CoworkAttachmentChip> Attachments => _attachments;

    /// <summary>Gets the motion setting. Reduced removes non-essential motion on both heads.</summary>
    public MotionPreference Motion { get; }

    /// <summary>Gets whether reduced motion is in force for this conversation.</summary>
    public bool ReducedMotion => Motion == MotionPreference.Reduced;

    /// <summary>Gets or sets whether the selected model reads images. Changing it updates chips in place.</summary>
    public bool ReadsImages
    {
        get => _readsImages;
        set
        {
            if (_readsImages == value)
            {
                return;
            }

            _readsImages = value;
            RefreshImagePolicy();
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ImageToolsEnabled));
            NotifyPropertyChanged(nameof(ImageToolMessage));
            NotifyPropertyChanged(nameof(CanAttachImages));
            NotifyPropertyChanged(nameof(CanSend));
        }
    }

    /// <summary>Gets whether image tools are offered. A model that reads no images turns them off.</summary>
    public bool ImageToolsEnabled => _readsImages;

    /// <summary>Gets the image-tool chip message, or null when image tools are on.</summary>
    public string? ImageToolMessage => _readsImages ? null : ModelCannotReadImages;

    /// <summary>Gets whether an image may be attached.</summary>
    public bool CanAttachImages => _readsImages;

    /// <summary>Gets whether send is blocked by an attachment reason.</summary>
    public bool CanSend => _attachments.All(static chip => !chip.BlocksSend);

    /// <summary>
    /// Adds an attachment chip. A missing file or an image over <see cref="ImageLimits"/> shows the reason on the chip
    /// and blocks send. A model that reads no images disables an image chip with <see cref="ModelCannotReadImages"/>.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="isImage">Whether the attachment is an image.</param>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="exists">Whether the file exists.</param>
    /// <returns>The chip.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public CoworkAttachmentChip AddAttachment(string name, bool isImage, long bytes, bool exists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var limit = LimitReason(isImage, bytes);
        var reason = !exists
            ? MissingFile
            : limit ?? (_readsImages || !isImage ? null : ModelCannotReadImages);
        var chip = new CoworkAttachmentChip(name, isImage, bytes, exists, limit, reason);
        _attachments.Add(chip);
        NotifyPropertyChanged(nameof(Attachments));
        NotifyPropertyChanged(nameof(CanSend));
        return chip;
    }

    /// <summary>Removes an attachment chip.</summary>
    /// <param name="id">The chip identifier.</param>
    /// <returns>True when a chip was removed.</returns>
    public bool RemoveAttachment(string id)
    {
        var index = _attachments.FindIndex(chip => string.Equals(chip.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _attachments.RemoveAt(index);
        NotifyPropertyChanged(nameof(Attachments));
        NotifyPropertyChanged(nameof(CanSend));
        return true;
    }

    /// <summary>Sends the draft unless an attachment blocks send.</summary>
    /// <returns>True when the composer accepted the attempt.</returns>
    public async Task<bool> TrySendAsync()
    {
        if (!CanSend)
        {
            return false;
        }

        await Composer.SendAsync().ConfigureAwait(true);
        return true;
    }

    /// <summary>Steers the running turn with the current draft. A blocked attachment does not steer.</summary>
    /// <returns>A task that completes when the host has handled the steer, or immediately when there is no channel.</returns>
    public async Task SteerAsync()
    {
        if (!CanSend || _onSteer is null)
        {
            return;
        }

        await _onSteer(Composer.Draft).ConfigureAwait(true);
    }

    /// <summary>Retries a recoverable failure.</summary>
    /// <returns>A task that completes when the host has handled the retry, or immediately when retry is unavailable.</returns>
    public async Task RetryAsync()
    {
        if (!Session.CanRetry || _onRetry is null)
        {
            return;
        }

        await _onRetry().ConfigureAwait(true);
    }

    private string? LimitReason(bool isImage, long bytes)
    {
        if (!isImage)
        {
            return null;
        }

        var limits = ImageLimits;
        if (bytes > limits.MaxBytes)
        {
            return AttachmentCopy.TooLarge(limits.MaxBytes);
        }

        var images = _attachments.Count(static chip => chip.IsImage);
        if (images >= limits.MaxCount)
        {
            return AttachmentCopy.TooMany(limits.MaxCount);
        }

        var total = _attachments.Where(static chip => chip.IsImage).Sum(static chip => chip.Bytes) + bytes;
        return total > limits.MaxTotalBytes ? AttachmentCopy.TotalTooLarge(limits.MaxTotalBytes) : null;
    }

    private void RefreshImagePolicy()
    {
        foreach (var chip in _attachments.Where(static chip => chip.IsImage))
        {
            var reason = !chip.Exists
                ? MissingFile
                : chip.LimitReason ?? (_readsImages ? null : ModelCannotReadImages);
            chip.SetReason(reason);
        }

        NotifyPropertyChanged(nameof(Attachments));
        NotifyPropertyChanged(nameof(CanSend));
    }
}

