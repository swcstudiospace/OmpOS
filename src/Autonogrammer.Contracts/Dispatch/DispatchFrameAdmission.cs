namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Result of admitting one text frame. A rejected frame still reports its <c>t</c> when one was read, but only an unknown <c>t</c>
/// carries <see cref="DispatchFrameTypes.UnsupportedFrame"/>; a known <c>t</c> that fails validation has no error code.
/// </summary>
/// <param name="Accepted">Whether the frame is one object with a known <c>t</c> and <c>v</c>.</param>
/// <param name="Type">The <c>t</c> value when one was read, known or not, whether or not the frame was accepted.</param>
/// <param name="ErrCode"><see cref="DispatchFrameTypes.UnsupportedFrame"/> when <c>t</c> is unknown; null for an accepted frame and for a known <c>t</c> that failed validation.</param>
public readonly record struct DispatchFrameAdmission(bool Accepted, string? Type, string? ErrCode);
