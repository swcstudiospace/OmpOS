namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>The lines one poll of a <see cref="SwarmEventTail"/> found.</summary>
/// <param name="Status">Whether the file exists and could be read.</param>
/// <param name="Lines">The complete, non-empty, within-cap lines read this poll, in file order, without the line terminator. They are slices of one buffer that is owned by this result.</param>
internal readonly record struct TailPoll(FileReadStatus Status, IReadOnlyList<ReadOnlyMemory<byte>> Lines);
