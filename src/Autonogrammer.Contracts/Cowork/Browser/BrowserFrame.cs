#pragma warning disable MA0048 // The plan owns this file for the frame and its metadata.
namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// One JPEG frame from <c>Page.screencastFrame</c>, plus the metadata a take-over uses to map CSS pixels.
/// </summary>
/// <param name="Jpeg">The JPEG bytes. The pump does not re-encode them.</param>
/// <param name="Metadata">The frame metadata.</param>
/// <param name="SessionId">The screencast session id, as text.</param>
public sealed record BrowserFrame(ReadOnlyMemory<byte> Jpeg, BrowserFrameMetadata Metadata, string SessionId);

/// <summary>
/// The <c>screencastFrame.metadata</c> fields the pane needs. Device size is in DIP. Scroll is in CSS pixels.
/// </summary>
/// <param name="OffsetTop">The top offset in DIP.</param>
/// <param name="PageScaleFactor">The page scale factor. Zero is treated as one by the take-over mapper.</param>
/// <param name="DeviceWidth">The device width in DIP.</param>
/// <param name="DeviceHeight">The device height in DIP.</param>
/// <param name="ScrollOffsetX">The horizontal scroll in CSS pixels.</param>
/// <param name="ScrollOffsetY">The vertical scroll in CSS pixels.</param>
/// <param name="Timestamp">The frame swap timestamp.</param>
public sealed record BrowserFrameMetadata(
    double OffsetTop,
    double PageScaleFactor,
    double DeviceWidth,
    double DeviceHeight,
    double ScrollOffsetX,
    double ScrollOffsetY,
    double Timestamp);
