namespace OmpOS.Presentation.Cloud;

/// <summary>One step of the status history of an agent, ready to draw.</summary>
/// <param name="Status">The status that was entered.</param>
/// <param name="StatusText">The status in words.</param>
/// <param name="IconKey">The glyph key of the status.</param>
/// <param name="TimeText">The relative time the status was entered, for example <c>5 min ago</c>.</param>
/// <param name="Note">The reason the host reported, or an empty string.</param>
/// <param name="AccessibleName">The accessible name: the status, the time and the note.</param>
public sealed record CloudTimelineRow(CloudAgentStatus Status, string StatusText, string IconKey, string TimeText, string Note, string AccessibleName);
