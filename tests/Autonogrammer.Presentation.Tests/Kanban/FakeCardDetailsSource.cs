using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>A Substrate details source the test scripts per session.</summary>
internal sealed class FakeCardDetailsSource : IKanbanCardDetailsSource
{
    /// <summary>Gets the answer of each session. A session without an entry reads null, which is what an unreachable Substrate gives.</summary>
    internal Dictionary<string, KanbanCardDetails?> Answers { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets an exception every read throws, or null.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets the sessions read, in order.</summary>
    internal List<string> Reads { get; } = [];

    /// <inheritdoc />
    public Task<KanbanCardDetails?> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reads.Add(sessionId);
        if (Failure is not null)
        {
            return Task.FromException<KanbanCardDetails?>(Failure);
        }

        return Task.FromResult(Answers.GetValueOrDefault(sessionId));
    }
}
