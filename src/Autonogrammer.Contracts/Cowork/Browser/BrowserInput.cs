#pragma warning disable MA0048 // The plan owns this file for both input records.
namespace Autonogrammer.Contracts.Cowork.Browser;

/// <summary>
/// A pointer event in the displayed frame. The controller maps it to CSS pixels with the frame metadata.
/// </summary>
/// <param name="X">The x position in the displayed frame, in pixels.</param>
/// <param name="Y">The y position in the displayed frame, in pixels.</param>
/// <param name="FrameWidth">The displayed frame width. Must be positive.</param>
/// <param name="FrameHeight">The displayed frame height. Must be positive.</param>
/// <param name="Type">The CDP mouse event type, such as <c>mousePressed</c>.</param>
/// <param name="Button">The CDP button name, such as <c>left</c>.</param>
public sealed record BrowserPointerInput(double X, double Y, int FrameWidth, int FrameHeight, string Type, string Button);

/// <summary>
/// A key event. <see cref="Text"/> is sent with <c>Input.insertText</c>. <see cref="Type"/> is sent with <c>Input.dispatchKeyEvent</c>.
/// </summary>
/// <param name="Key">The key name.</param>
/// <param name="Text">The text to insert, or null when this event is only a key.</param>
/// <param name="Type">The CDP key event type, such as <c>keyDown</c>.</param>
public sealed record BrowserKeyInput(string Key, string? Text, string Type);
