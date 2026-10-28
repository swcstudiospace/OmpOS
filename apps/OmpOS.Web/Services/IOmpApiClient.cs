using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Web.Services;

/// <summary>
/// The typed client of the OmpOS API: one method per route the web head uses. Every method takes a cancellation token, maps a problem response
/// to <see cref="ApiProblemException"/> (404, 409, 413, 415, 422, 429 and the rest by <see cref="ApiProblemException.Kind"/>) and never puts a
/// prompt, a request body or a credential into an exception message.
/// </summary>
internal interface IOmpApiClient
{
    /// <summary>Gets the session catalogue (<c>GET /sessions</c>), most recently active first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The caller's sessions.</returns>
    Task<IReadOnlyList<SessionCatalogEntry>> ListSessionsAsync(CancellationToken cancellationToken);

    /// <summary>Creates a session (<c>POST /sessions</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new session.</returns>
    Task<Session> CreateSessionAsync(CancellationToken cancellationToken);

    /// <summary>Gets one session with its message and event log (<c>GET /sessions/{id}</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session.</returns>
    Task<Session> GetSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Submits a prompt (<c>POST /sessions/{id}/prompts</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="prompt">The prompt text. It is sent and never logged.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the API accepted the prompt.</returns>
    Task SubmitPromptAsync(string sessionId, string prompt, CancellationToken cancellationToken);

    /// <summary>Sends a steering message to the running turn (<c>POST /sessions/{id}/steer</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="text">The message. It is sent and never logged.</param>
    /// <param name="mode">Send now, or queue behind the turn.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How the message was delivered.</returns>
    Task<SteerResponse> SteerAsync(string sessionId, string text, SteerMode mode, CancellationToken cancellationToken);

    /// <summary>Lists the pending tool approvals (<c>GET /sessions/{id}/tool-approvals</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending requests.</returns>
    Task<PendingToolApprovalsResponse> GetToolApprovalsAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Decides one tool approval (<c>POST /sessions/{id}/tool-approvals/{requestId}</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="requestId">The approval request.</param>
    /// <param name="allow">True to allow, false to deny.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the decision was recorded.</returns>
    Task DecideToolApprovalAsync(string sessionId, string requestId, bool allow, CancellationToken cancellationToken);

    /// <summary>Streams one session's events (<c>GET /sessions/{id}/events</c>) with resume, until the terminal event or cancellation.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="sinceSeq">The first sequence to include, or null for the head of the log.</param>
    /// <param name="cancellationToken">Stops the stream.</param>
    /// <returns>The events, each once.</returns>
    IAsyncEnumerable<StreamEvent> StreamSessionAsync(string sessionId, long? sinceSeq, CancellationToken cancellationToken);

    /// <summary>Gets the stage timeline (<c>GET /sessions/{id}/stages</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stages and whether a plugin published any.</returns>
    Task<SessionStagesResponse> GetStagesAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Gets the task context (<c>GET /sessions/{id}/context</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The context, or <c>available: false</c> with a reason.</returns>
    Task<SessionContextResponse> GetContextAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Gets the engine options (<c>GET /sessions/{id}/config-options</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The model, mode and thinking options.</returns>
    Task<SessionConfigOptionsResponse> GetConfigOptionsAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Changes one option (<c>PUT /sessions/{id}/config-options/{optionId}</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="optionId">The option.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The options after the change.</returns>
    Task<SessionConfigOptionsResponse> SetConfigOptionAsync(string sessionId, string optionId, string value, CancellationToken cancellationToken);

    /// <summary>Gets the slash commands the engine advertises (<c>GET /sessions/{id}/commands</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The commands.</returns>
    Task<SessionCommandsResponse> GetCommandsAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Lists the files the agent changed (<c>GET /sessions/{id}/changes</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The changes with their before and after text and review state.</returns>
    Task<SessionChangesResponse> GetChangesAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Accepts or rejects one change (<c>POST /sessions/{id}/changes/{path}/accept|reject</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="path">The file path as <see cref="OmpOS.Contracts.Controls.SessionChange.Path"/> reports it. It is URL-encoded by the client.</param>
    /// <param name="accept">True to accept, false to reject.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The decision and whether it was applied to the workspace.</returns>
    Task<ChangeDecisionResponse> DecideChangeAsync(string sessionId, string path, bool accept, CancellationToken cancellationToken);

    /// <summary>Gets the board (<c>GET /board</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The ten columns with their cards.</returns>
    Task<BoardResponse> GetBoardAsync(CancellationToken cancellationToken);

    /// <summary>Gets the card details of a session (<c>GET /sessions/{id}/card-details</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The details, or <c>available: false</c> with a reason.</returns>
    Task<CardDetailsResponse> GetCardDetailsAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Closes a session (<c>POST /sessions/{id}/close</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session's new row state.</returns>
    Task<SessionLifecycleResponse> CloseSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Reopens a closed session (<c>POST /sessions/{id}/reopen</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session's new row state.</returns>
    Task<SessionLifecycleResponse> ReopenSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Resumes a lost session (<c>POST /sessions/{id}/resume</c>).</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session's new row state.</returns>
    Task<SessionLifecycleResponse> ResumeSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Gets the workspace name and plugin check (<c>GET /workspace</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The workspace.</returns>
    Task<WorkspaceInfo> GetWorkspaceAsync(CancellationToken cancellationToken);

    /// <summary>Gets the licence and source offer (<c>GET /source</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The source information.</returns>
    Task<SourceInfo> GetSourceAsync(CancellationToken cancellationToken);

    /// <summary>Probes the API (<c>GET /health/ready</c>).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the API answered 2xx; false when it answered another status. A failure to connect throws.</returns>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
