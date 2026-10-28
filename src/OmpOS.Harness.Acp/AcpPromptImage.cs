namespace OmpOS.Harness.Acp;

/// <summary>A neutral, already-validated image attached to a prompt. The wiring converts the composer attachment to this type, because this assembly does not reference presentation.</summary>
/// <param name="Name">A short display name used in omission notes; never sent as content.</param>
/// <param name="MimeType">The MIME type: <c>image/png</c>, <c>image/jpeg</c>, <c>image/gif</c> or <c>image/webp</c>.</param>
/// <param name="Data">The encoded image bytes.</param>
public sealed record AcpPromptImage(string Name, string MimeType, ReadOnlyMemory<byte> Data);
