using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Contracts.Runs;

/// <summary>
/// The part of a session's record that belongs to one run: the messages and the stream events of that run's turn, in the order they were recorded.
/// </summary>
public sealed record RunTranscript
{
    /// <summary>Gets the run's chat messages, starting with the user prompt.</summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>Gets the run's stream events, starting with the prompt acceptance event.</summary>
    public required IReadOnlyList<StreamEvent> Events { get; init; }
}
