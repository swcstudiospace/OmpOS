using Autonogrammer.Contracts.Controls;
using Autonogrammer.Presentation.Runs;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="IRunInput"/> of one session: a steering message and a queued follow-up both go to <c>POST /sessions/{id}/steer</c>. The API
/// holds a steer until the turn ends when the engine cannot take it live, and reports that as a delivery, so nothing is counted held here.
/// </summary>
internal sealed class WebRunInput : IRunInput
{
    private readonly IOmpApiClient _api;
    private readonly string _sessionId;

    /// <summary>Initializes a new instance of the <see cref="WebRunInput"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="sessionId">The session.</param>
    internal WebRunInput(IOmpApiClient api, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _api = api;
        _sessionId = sessionId;
    }

    /// <inheritdoc />
    public int SteersHeld => 0;

    /// <inheritdoc />
    public async Task SteerAsync(string text, CancellationToken cancellationToken) =>
        _ = await _api.SteerAsync(_sessionId, text, SteerMode.SendNow, cancellationToken).ConfigureAwait(true);

    /// <inheritdoc />
    public async Task FollowUpAsync(string text, CancellationToken cancellationToken) =>
        _ = await _api.SteerAsync(_sessionId, text, SteerMode.Queue, cancellationToken).ConfigureAwait(true);
}
