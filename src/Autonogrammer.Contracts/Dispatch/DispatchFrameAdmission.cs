namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Result of admitting one text frame. An unknown <c>t</c> carries <see cref="DispatchFrameTypes.UnsupportedFrame"/>.</summary>
/// <param name="Accepted">Whether the frame is one object with a known <c>t</c> and <c>v</c>.</param>
/// <param name="Type">The <c>t</c> value when one was present.</param>
/// <param name="ErrCode"><see cref="DispatchFrameTypes.UnsupportedFrame"/> when <c>t</c> is unknown; otherwise null.</param>
public readonly record struct DispatchFrameAdmission(bool Accepted, string? Type, string? ErrCode);
