using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Cowork.Sync;

/// <summary>
/// The server surface <see cref="CoworkSessionSync"/> calls. Routes are added by a later plan; this is the client port only.
/// </summary>
public interface ICoworkSessionApi
{
    /// <summary>Sends <c>PUT /cowork/sessions/{id}</c> for one summary.</summary>
    /// <param name="summary">The summary. It carries no messages, paths or trusted folders.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the server accepted the summary.</returns>
    Task<CoworkSessionApiResult> PutSummaryAsync(CoworkSessionSummary summary, CancellationToken cancellationToken);

    /// <summary>Sends <c>DELETE /cowork/sessions/{id}</c>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the server accepted the delete.</returns>
    Task<CoworkSessionApiResult> DeleteAsync(string sessionId, CancellationToken cancellationToken);
}

