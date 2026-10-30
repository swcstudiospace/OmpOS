namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>The result of reading a byte range of a growing file.</summary>
/// <param name="Status">What happened.</param>
/// <param name="FileLength">The length of the file when it was opened. Zero unless the file was opened.</param>
/// <param name="StartOffset">The offset the content starts at. It differs from the requested offset when the read jumped to the tail.</param>
/// <param name="Content">The bytes read.</param>
/// <param name="JumpedToTail">True when the requested offset was further than one chunk behind the end of the file and the read started near the end instead.</param>
internal readonly record struct RangeReadResult(FileReadStatus Status, long FileLength, long StartOffset, ReadOnlyMemory<byte> Content, bool JumpedToTail);
