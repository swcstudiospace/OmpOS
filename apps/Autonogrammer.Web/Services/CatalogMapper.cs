using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Web.Services;

/// <summary>Maps the API's catalogue rows to the Presentation records. The API names the rail status itself (<c>rowStatus</c>); the mapping picks the lifecycle and live status that make <c>AgentRowStatusMap</c> resolve to that same status.</summary>
internal static class CatalogMapper
{
    /// <summary>The longest title derived from the first prompt, in characters.</summary>
    internal const int MaxTitleChars = 48;

    /// <summary>Maps a catalogue row to the list-row view the rail model reads.</summary>
    /// <param name="entry">The API row.</param>
    /// <returns>The summary.</returns>
    internal static SessionSummary ToSummary(SessionCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new SessionSummary
        {
            Id = entry.Id,
            Title = TitleOf(entry),
            Lifecycle = LifecycleOf(entry.RowStatus),
            CreatedAt = entry.CreatedAt,
            LastActivity = entry.LastActivityAt,
            GraphId = entry.GraphId,
            Kind = entry.Kind == CatalogSessionKind.Terminal ? SessionKind.Terminal : SessionKind.Chat,
        };
    }

    /// <summary>Gets the persisted lifecycle that, with <see cref="LiveStatusOf"/> and the closed flag, resolves to <paramref name="status"/>.</summary>
    /// <param name="status">The API's rail status.</param>
    /// <returns>The lifecycle. An unknown status maps to a value no lifecycle defines, which the rail shows as "Status unknown".</returns>
    internal static SessionLifecycle LifecycleOf(CatalogRowStatus status) => status switch
    {
        CatalogRowStatus.NeedsApproval or CatalogRowStatus.WaitingForYou or CatalogRowStatus.Running => SessionLifecycle.Running,
        CatalogRowStatus.Queued or CatalogRowStatus.Ready => SessionLifecycle.Created,
        CatalogRowStatus.Lost => SessionLifecycle.Lost,
        CatalogRowStatus.Paused => SessionLifecycle.Paused,
        CatalogRowStatus.Closed or CatalogRowStatus.Completed => SessionLifecycle.Completed,
        CatalogRowStatus.Failed => SessionLifecycle.Failed,
        _ => (SessionLifecycle)(-1),
    };

    /// <summary>Gets the live runtime status the rail reads for a created or running session.</summary>
    /// <param name="status">The API's rail status.</param>
    /// <returns>The runtime status, or null when the persisted lifecycle alone decides the row.</returns>
    internal static RuntimeStatus? LiveStatusOf(CatalogRowStatus status) => status switch
    {
        CatalogRowStatus.NeedsApproval => RuntimeStatus.NeedsAttention,
        CatalogRowStatus.WaitingForYou => RuntimeStatus.AwaitingInput,
        CatalogRowStatus.Running => RuntimeStatus.Running,
        CatalogRowStatus.Queued => RuntimeStatus.Queued,
        _ => null,
    };

    /// <summary>Derives the rail title: the first prompt, shortened, for a chat; <c>omp terminal {id}</c> for a terminal; <c>Agent {id}</c> when there is no prompt yet.</summary>
    /// <param name="entry">The API row.</param>
    /// <returns>The title.</returns>
    internal static string TitleOf(SessionCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var shortId = entry.Id.Length <= 8 ? entry.Id : entry.Id[..8];
        if (entry.Kind == CatalogSessionKind.Terminal)
        {
            return $"omp terminal {shortId}";
        }

        var first = entry.Messages.FirstOrDefault(static m => string.Equals(m.Role, "user", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(m.Content));
        if (first is null)
        {
            return $"Agent {shortId}";
        }

        var singleLine = string.Join(' ', first.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= MaxTitleChars ? singleLine : singleLine[..MaxTitleChars] + "\u2026";
    }

    /// <summary>Maps the stored session status to whether a run is active.</summary>
    /// <param name="status">The stored status.</param>
    /// <returns>True for a running turn or a turn waiting for an approval.</returns>
    internal static bool IsRunActive(SessionStatus status) => status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;
}
