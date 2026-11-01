namespace Autonogrammer.Native.Cowork;

/// <summary>The result of attaching images.</summary>
/// <param name="Sent">Whether the prompt may be sent.</param>
/// <param name="Reason">The refusal or limit reason.</param>
/// <param name="ChipText">The text shown on the chip.</param>
internal sealed record CoworkAttachmentResult(bool Sent, string? Reason, string? ChipText);
