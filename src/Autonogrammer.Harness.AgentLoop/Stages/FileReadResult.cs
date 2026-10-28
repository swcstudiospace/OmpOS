namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>The result of reading a whole file under a size cap.</summary>
/// <param name="Status">What happened.</param>
/// <param name="Content">The bytes read. Empty unless <paramref name="Status"/> is <see cref="FileReadStatus.Ok"/>.</param>
/// <param name="Error">The exception behind <see cref="FileReadStatus.Unreadable"/>, or null.</param>
internal readonly record struct FileReadResult(FileReadStatus Status, ReadOnlyMemory<byte> Content, Exception? Error);
