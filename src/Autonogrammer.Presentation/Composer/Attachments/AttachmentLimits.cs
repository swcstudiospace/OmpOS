namespace Autonogrammer.Presentation.Composer.Attachments;

/// <summary>The size limits of image attachments.</summary>
/// <param name="MaxCount">The most images one message carries.</param>
/// <param name="MaxBytes">The largest single image in bytes.</param>
/// <param name="MaxTotalBytes">The largest sum of all images in bytes.</param>
public sealed record AttachmentLimits(int MaxCount = 8, long MaxBytes = 10L * 1024 * 1024, long MaxTotalBytes = 25L * 1024 * 1024);
