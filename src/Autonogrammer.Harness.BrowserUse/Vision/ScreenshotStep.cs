namespace Autonogrammer.Harness.BrowserUse.Vision;

/// <summary>One capture attempt. Format, quality and clip scale are CDP parameters only.</summary>
/// <param name="Format">png or jpeg.</param>
/// <param name="Quality">JPEG quality, or null for PNG.</param>
/// <param name="Scale">The clip scale.</param>
/// <param name="Viewport">desktop, tablet or mobile.</param>
/// <param name="Zoom">Whether this is a full-resolution region.</param>
public sealed record ScreenshotStep(string Format, int? Quality, double Scale, string Viewport, bool Zoom);
