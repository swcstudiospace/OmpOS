using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The Web <see cref="ICoworkSessionCatalog"/>. It reads <c>GET account/sessions</c>, keeps rows labelled cowork, and never reads a transcript.
/// </summary>
internal sealed class WebCoworkSessionCatalog : ICoworkSessionCatalog
{
    private readonly IOmpApiClient _api;
    private CoworkSessionSummary[] _rows = [];

    /// <summary>Creates the catalog. It loads nothing until <see cref="LoadAsync"/>.</summary>
    /// <param name="api">The API client.</param>
    internal WebCoworkSessionCatalog(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets whether the last load failed.</summary>
    public bool HasFailed { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<CoworkSessionSummary> Snapshot() => _rows;

    /// <summary>Loads the account session list and keeps the Cowork rows.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the list has been applied or the failure recorded.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var list = await _api.GetAccountSessionsAsync(cancellationToken).ConfigureAwait(false);
            _rows = list.Items
                .Where(static item => string.Equals(item.Surface, "cowork", StringComparison.OrdinalIgnoreCase))
                .Select(static item => Map(item.Session))
                .ToArray();
            HasFailed = false;
        }
        catch (Exception fault) when (fault is not OperationCanceledException and not OutOfMemoryException)
        {
            HasFailed = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Loads the list again.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the reload ended.</returns>
    public Task RetryAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    private static CoworkSessionSummary Map(SessionSummary session) => new()
    {
        Id = session.Id,
        Title = session.Title,
        Status = MapStatus(session.Lifecycle),
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.LastActivity,
        ProjectId = session.GraphId,
        DeviceId = null,
    };

    private static CoworkSessionStatus MapStatus(SessionLifecycle lifecycle) => lifecycle switch
    {
        SessionLifecycle.Created => CoworkSessionStatus.Created,
        SessionLifecycle.Running => CoworkSessionStatus.Running,
        SessionLifecycle.Paused => CoworkSessionStatus.Paused,
        SessionLifecycle.Completed => CoworkSessionStatus.Completed,
        SessionLifecycle.Failed => CoworkSessionStatus.Failed,
        SessionLifecycle.Lost => CoworkSessionStatus.Lost,
        _ => CoworkSessionStatus.Created,
    };
}
