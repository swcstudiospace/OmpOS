using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Sessions;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Terminal;

namespace OmpOS.Web.Services;

/// <summary>
/// The <see cref="IAgentPanelHost"/> of the web head. The API owns the engines and the registry; the host asks it to create, close and list
/// sessions and keeps <see cref="WebSessionCatalog"/> current. An omp terminal cannot be created from a browser: that operation fails with the
/// reason the head shows next to the New terminal button.
/// </summary>
internal sealed class WebAgentPanelHost : IAgentPanelHost
{
    private readonly IOmpApiClient _api;
    private readonly WebSessionCatalog _catalog;
    private readonly Func<AgentRow, AgentSessionViewModel> _open;
    private readonly Func<CancellationToken, Task> _reload;

    /// <summary>Initializes a new instance of the <see cref="WebAgentPanelHost"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="catalog">The catalogue the host keeps current.</param>
    /// <param name="open">Builds the view model of a session the catalogue lists.</param>
    /// <param name="reload">Reloads the catalogue, the workspace and the source.</param>
    internal WebAgentPanelHost(
        IOmpApiClient api,
        WebSessionCatalog catalog,
        Func<AgentRow, AgentSessionViewModel> open,
        Func<CancellationToken, Task> reload)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(reload);
        _api = api;
        _catalog = catalog;
        _open = open;
        _reload = reload;
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var created = await _api.CreateSessionAsync(cancellationToken).ConfigureAwait(true);
        var listed = await _api.ListSessionsAsync(cancellationToken).ConfigureAwait(true);
        IReadOnlyList<SessionCatalogEntry> entries = listed;
        if (!listed.Any(entry => string.Equals(entry.Id, created.Id, StringComparison.Ordinal)))
        {
            entries = [.. listed, EntryOf(created)];
        }

        _catalog.Replace(entries);
        return created.Id;
    }

    /// <inheritdoc />
    public Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken) =>
        Task.FromException<string>(new InvalidOperationException(TerminalAvailability.WebReason));

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _open(row);
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var response = await _api.CloseSessionAsync(sessionId, cancellationToken).ConfigureAwait(true);
        _ = _catalog.Apply(response);
    }

    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken) => _reload(cancellationToken);

    private static SessionCatalogEntry EntryOf(Session created) => new()
    {
        Id = created.Id,
        OwnerId = created.OwnerId,
        CreatedAt = created.CreatedAt,
        UpdatedAt = created.UpdatedAt,
        Status = created.Status,
        SandboxId = created.SandboxId,
        Messages = created.Messages,
        Events = created.Events,
        Kind = CatalogSessionKind.Agent,
        RowStatus = CatalogRowStatus.Ready,
        Group = CatalogRowGroup.Idle,
        LastActivityAt = created.UpdatedAt,
        Closed = false,
    };
}
