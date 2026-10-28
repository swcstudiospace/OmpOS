namespace OmpOS.Harness.Acp;

/// <summary>Size limits applied when @-mention resources are embedded in a prompt.</summary>
/// <param name="MaxResourceBytes">The most UTF-8 bytes of text kept from one resource.</param>
/// <param name="MaxTotalBytes">The most UTF-8 bytes of text kept across all resources.</param>
/// <param name="MaxResources">The most resources embedded; later ones are dropped.</param>
public sealed record AcpPromptLimits(int MaxResourceBytes = 262144, int MaxTotalBytes = 1048576, int MaxResources = 32);
