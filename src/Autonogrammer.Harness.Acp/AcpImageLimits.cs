namespace Autonogrammer.Harness.Acp;

/// <summary>Size limits applied when images are attached to a prompt; they equal the composer's limits.</summary>
/// <param name="MaxImages">The most images sent; later ones are dropped.</param>
/// <param name="MaxImageBytes">The largest single image in bytes.</param>
/// <param name="MaxTotalBytes">The most image bytes across the prompt.</param>
public sealed record AcpImageLimits(int MaxImages = 8, long MaxImageBytes = 10L * 1024 * 1024, long MaxTotalBytes = 25L * 1024 * 1024);
