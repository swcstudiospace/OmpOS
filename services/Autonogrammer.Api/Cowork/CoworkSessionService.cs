using System.Collections.Concurrent;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork;

/// <summary>
/// Stores Cowork summaries as session rows whose surface is fixed to <see cref="SessionSurface.Cowork"/>.
/// The owner is the caller. The row holds an empty message list and no events. A delete closes the row:
/// the repository has no delete, so a closed Cowork row is the removed summary and does not count toward the cap.
/// </summary>
internal sealed class CoworkSessionService
{
    private readonly ISessionRepository _repository;
    private readonly IOptions<CoworkOptions> _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _owners = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CoworkSessionSummary> _summaries = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="CoworkSessionService"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="options">The Cowork bounds.</param>
    public CoworkSessionService(ISessionRepository repository, IOptions<CoworkOptions> options)
    {
        _repository = repository;
        _options = options;
    }

    /// <summary>Stores a summary for <paramref name="ownerId"/>. A body field is never the owner or the surface.</summary>
    /// <param name="sessionId">The route identifier.</param>
    /// <param name="summary">The summary. Its title is already capped by the contract.</param>
    /// <param name="ownerId">The authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the store.</param>
    /// <returns>The outcome and the stored summary when the store succeeded.</returns>
    public async Task<(CoworkPutOutcome Outcome, CoworkSessionSummary? Stored)> PutAsync(
        string sessionId,
        CoworkSessionSummary summary,
        string ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var gate = _owners.GetOrAdd(ownerId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (existing is not null && !IsOpenCoworkOf(existing, ownerId))
            {
                return (CoworkPutOutcome.NotFound, null);
            }

            var options = _options.Value;
            if (existing is null)
            {
                var held = await _repository.ListByOwnerAsync(ownerId, SessionSurface.Cowork, includeClosed: false, options.MaxSessionsPerOwner, cursor: null, cancellationToken).ConfigureAwait(false);
                if (held.Count >= options.MaxSessionsPerOwner)
                {
                    return (CoworkPutOutcome.Conflict, null);
                }
            }

            var title = summary.Title.Length <= options.MaxTitleLength ? summary.Title : summary.Title[..options.MaxTitleLength];
            var stored = summary with { Id = sessionId, Title = title };
            var session = new Session
            {
                Id = sessionId,
                OwnerId = ownerId,
                CreatedAt = existing?.CreatedAt ?? stored.CreatedAt,
                UpdatedAt = stored.UpdatedAt,
                Status = ToSessionStatus(stored.Status),
                Surface = SessionSurface.Cowork,
                Messages = [],
                Events = Array.Empty<StreamEvent>(),
            };
            await _repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            _summaries[sessionId] = stored;
            return (existing is null ? CoworkPutOutcome.Created : CoworkPutOutcome.Updated, stored);
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>Removes one summary. An absent row and another owner's row are both false.</summary>
    /// <param name="sessionId">The route identifier.</param>
    /// <param name="ownerId">The authenticated caller.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns><see langword="true"/> when this call removed the summary.</returns>
    public async Task<bool> DeleteAsync(string sessionId, string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        var existing = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (existing is null || !IsOpenCoworkOf(existing, ownerId))
        {
            return false;
        }

        _ = await _repository.SetClosedAsync(sessionId, closed: true, cancellationToken).ConfigureAwait(false);
        _ = _summaries.TryRemove(sessionId, out _);
        return true;
    }

    private static bool IsOpenCoworkOf(Session session, string ownerId) =>
        string.Equals(session.OwnerId, ownerId, StringComparison.Ordinal)
        && session.Surface == SessionSurface.Cowork
        && session.ClosedAt is null;

    private static SessionStatus ToSessionStatus(CoworkSessionStatus status) => status switch
    {
        CoworkSessionStatus.Running => SessionStatus.Running,
        CoworkSessionStatus.WaitingForDecision => SessionStatus.AwaitingToolApproval,
        CoworkSessionStatus.Completed => SessionStatus.Completed,
        CoworkSessionStatus.Failed => SessionStatus.Failed,
        CoworkSessionStatus.Lost => SessionStatus.Interrupted,
        CoworkSessionStatus.Paused => SessionStatus.Cancelled,
        _ => SessionStatus.Idle,
    };
}
