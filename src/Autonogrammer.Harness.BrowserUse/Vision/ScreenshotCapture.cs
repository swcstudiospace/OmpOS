namespace Autonogrammer.Harness.BrowserUse.Vision;

/// <summary>The ladder result.</summary>
/// <param name="Bytes">The image bytes.</param>
/// <param name="MimeType">The mime type.</param>
/// <param name="Steps">The attempts, in order.</param>
/// <param name="Failure">The failure, or null.</param>
public sealed record ScreenshotCapture(ReadOnlyMemory<byte> Bytes, string MimeType, IReadOnlyList<ScreenshotStep> Steps, string? Failure);
