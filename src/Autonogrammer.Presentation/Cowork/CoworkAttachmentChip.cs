using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>One attachment chip. A reason blocks send and is the text the chip shows.</summary>
public sealed class CoworkAttachmentChip : ObservableModel
{
    private string? _reason;

    /// <summary>Creates a chip.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="isImage">Whether the attachment is an image.</param>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="exists">Whether the file exists.</param>
    /// <param name="limitReason">The limit reason, kept when the model policy changes.</param>
    /// <param name="reason">The reason shown now, or null when the chip is clean.</param>
    internal CoworkAttachmentChip(string name, bool isImage, long bytes, bool exists, string? limitReason, string? reason)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = name;
        IsImage = isImage;
        Bytes = bytes;
        Exists = exists;
        LimitReason = limitReason;
        _reason = reason;
    }

    /// <summary>Gets the identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets whether the attachment is an image.</summary>
    public bool IsImage { get; }

    /// <summary>Gets the size in bytes.</summary>
    public long Bytes { get; }

    /// <summary>Gets whether the file exists.</summary>
    public bool Exists { get; }

    /// <summary>Gets the limit reason, independent of the model policy.</summary>
    public string? LimitReason { get; }

    /// <summary>Gets the reason shown on the chip, or null when the chip is clean.</summary>
    public string? Reason => _reason;

    /// <summary>Gets whether this chip blocks send.</summary>
    public bool BlocksSend => _reason is not null;

    /// <summary>Gets the accessible name of the chip.</summary>
    public string AccessibleName => _reason is null ? Name : Name + ", " + _reason;

    /// <summary>Gets the accessible name of the remove control.</summary>
    public string RemoveName => "Remove " + Name;

    /// <summary>Replaces the shown reason.</summary>
    /// <param name="reason">The new reason, or null when the chip is clean.</param>
    internal void SetReason(string? reason)
    {
        if (SetProperty(ref _reason, reason, nameof(Reason)))
        {
            NotifyPropertyChanged(nameof(BlocksSend));
            NotifyPropertyChanged(nameof(AccessibleName));
        }
    }
}
