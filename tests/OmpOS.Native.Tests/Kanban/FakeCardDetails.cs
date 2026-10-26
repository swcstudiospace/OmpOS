using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>A Substrate details source the view tests script per session; a session without an answer reads null.</summary>
internal sealed class FakeCardDetails : IKanbanCardDetailsSource
{
    /// <summary>Gets the answer of each session.</summary>
    internal Dictionary<string, KanbanCardDetails?> Answers { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the number of reads.</summary>
    internal int Reads { get; private set; }

    /// <inheritdoc />
    public Task<KanbanCardDetails?> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Answers.GetValueOrDefault(sessionId));
    }
}
