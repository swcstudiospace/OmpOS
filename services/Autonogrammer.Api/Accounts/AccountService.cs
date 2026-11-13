#pragma warning disable MA0048 // The store types live with the service that is their only product caller.
using System.Collections.Concurrent;
using System.Security.Claims;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Security;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Accounts;

/// <summary>One account row. It holds no email.</summary>
/// <param name="CreatedAt">When the row was created.</param>
/// <param name="LastSeenAt">When the account was last seen.</param>
internal sealed record AccountRow(DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

/// <summary>The accounts table and its in-memory twin. A write upserts <c>last_seen_at</c> only and never stores an email.</summary>
internal interface IAccountStore
{
    /// <summary>Creates the row or moves <c>last_seen_at</c>. Two callers of one id leave one row.</summary>
    /// <param name="id">The subject.</param>
    /// <param name="seenAt">The time of this sighting.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the row is stored.</returns>
    Task TouchAsync(string id, DateTimeOffset seenAt, CancellationToken cancellationToken);

    /// <summary>Reads one row.</summary>
    /// <param name="id">The subject.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when the subject has none.</returns>
    Task<AccountRow?> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>Counts the stored rows.</summary>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The number of rows.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken);
}

/// <summary>The in-memory twin of the accounts table.</summary>
internal sealed class InMemoryAccountStore : IAccountStore
{
    private readonly ConcurrentDictionary<string, AccountRow> _rows = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task TouchAsync(string id, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();
        _ = _rows.AddOrUpdate(
            id,
            static (_, seen) => new AccountRow(seen, seen),
            static (_, existing, seen) => existing with { LastSeenAt = seen },
            seenAt);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AccountRow?> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_rows.TryGetValue(id, out var row) ? row : null);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_rows.Count);
    }
}

/// <summary>The PostgreSQL accounts table. Statements use the store search path and never write an email.</summary>
internal sealed class PostgresAccountStore : IAccountStore
{
    private const string TouchSql =
        "INSERT INTO accounts (id, created_at, last_seen_at) VALUES (@id, @seen, @seen) " +
        "ON CONFLICT (id) DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at";

    private const string FindSql = "SELECT created_at, last_seen_at FROM accounts WHERE id = @id";

    private const string CountSql = "SELECT COUNT(*) FROM accounts";

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>Initializes a new instance of the <see cref="PostgresAccountStore"/> class.</summary>
    /// <param name="dataSource">The session-store data source. Its search path resolves <c>accounts</c>.</param>
    public PostgresAccountStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task TouchAsync(string id, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        using (var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
        {
            await AccountAdmission.EnsureRowAsync(connection, id, cancellationToken).ConfigureAwait(false);
        }

        var command = _dataSource.CreateCommand(TouchSql);
        try
        {
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("seen", seenAt);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<AccountRow?> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        var command = _dataSource.CreateCommand(FindSql);
        try
        {
            command.Parameters.AddWithValue("id", id);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                var created = await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
                var seen = await reader.GetFieldValueAsync<DateTimeOffset>(1, cancellationToken).ConfigureAwait(false);
                return new AccountRow(created, seen);
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        var command = _dataSource.CreateCommand(CountSql);
        try
        {
            var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return count is long value ? (int)value : 0;
        }
        finally
        {
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>The owner-scoped account reads.</summary>
internal sealed class AccountService
{
    private const int PageSize = 200;

    private readonly IAccountStore _accounts;
    private readonly ISessionRepository _sessions;
    private readonly IEnumerable<IAccountFeature> _features;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="AccountService"/> class.</summary>
    /// <param name="accounts">The account store.</param>
    /// <param name="sessions">The session store.</param>
    /// <param name="features">The registered account features.</param>
    /// <param name="time">The clock.</param>
    public AccountService(IAccountStore accounts, ISessionRepository sessions, IEnumerable<IAccountFeature> features, TimeProvider time)
    {
        _accounts = accounts;
        _sessions = sessions;
        _features = features;
        _time = time;
    }

    /// <summary>Reads the caller's account. A caller with no row still gets an answer; the timestamps are null.</summary>
    /// <param name="user">The caller.</param>
    /// <param name="httpContext">The request, whose validated token may carry an email claim.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account.</returns>
    public async Task<AccountResponse> GetAsync(IUserContext user, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(httpContext);
        var row = await _accounts.FindAsync(user.Name, cancellationToken).ConfigureAwait(false);
        return new AccountResponse(user.Name, EmailOf(httpContext.User), row?.CreatedAt, row?.LastSeenAt, FeatureKeys());
    }

    /// <summary>Lists one page of the caller's Code sessions and one page of their Cowork sessions, newest first, each labelled with its surface.</summary>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The merged list.</returns>
    public async Task<AccountSessionList> ListSessionsAsync(IUserContext user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var code = await PageAsync(user.Name, SessionSurface.Code, cancellationToken).ConfigureAwait(false);
        var cowork = await PageAsync(user.Name, SessionSurface.Cowork, cancellationToken).ConfigureAwait(false);
        var items = code.Concat(cowork)
            .OrderByDescending(static item => item.Session.LastActivity)
            .ThenByDescending(static item => item.Session.Id, StringComparer.Ordinal)
            .ToArray();
        return new AccountSessionList(items);
    }

    /// <summary>Reads one of the caller's sessions. Another owner's session and an absent id are both null.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when the caller cannot see it.</returns>
    public async Task<AccountSessionItem?> FindSessionAsync(string id, IUserContext user, CancellationToken cancellationToken)
    {
        var session = await SessionEndpoints.FindOwnedSessionAsync(id, _sessions, user, cancellationToken).ConfigureAwait(false);
        return session is null ? null : ToItem(session);
    }

    /// <summary>
    /// Builds the Kanban board of the caller's Code sessions. Phase 69 unit 5 later routes this call site through <c>CodeBoardSource</c>;
    /// until then the Code filter is the inline <see cref="SessionSurface.Code"/> listing.
    /// </summary>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The board.</returns>
    public Task<BoardResponse> BoardAsync(IUserContext user, CancellationToken cancellationToken) =>
        CatalogEndpoints.BuildBoardAsync(_sessions, user, _time, cancellationToken);

    private async Task<IReadOnlyList<AccountSessionItem>> PageAsync(string ownerId, SessionSurface surface, CancellationToken cancellationToken)
    {
        var page = await _sessions.ListByOwnerAsync(ownerId, surface, includeClosed: true, PageSize, cursor: null, cancellationToken).ConfigureAwait(false);
        return page.Select(ToItem).ToArray();
    }

    private string[] FeatureKeys() =>
        [.. _features.Where(static feature => feature.Enabled).Select(static feature => feature.Key).Distinct(StringComparer.Ordinal)];

    private static AccountSessionItem ToItem(Session session) =>
        new(
            new SessionSummary
            {
                Id = session.Id,
                Title = CatalogProjector.TitleOf(session.Id),
                Lifecycle = LifecycleOf(session),
                CreatedAt = session.CreatedAt,
                LastActivity = session.UpdatedAt,
            },
            SurfaceLabel(session.Surface));

    private static SessionLifecycle LifecycleOf(Session session) => session.Status switch
    {
        SessionStatus.Running or SessionStatus.AwaitingToolApproval => SessionLifecycle.Running,
        SessionStatus.Completed => SessionLifecycle.Completed,
        SessionStatus.Failed or SessionStatus.Interrupted => SessionLifecycle.Failed,
        SessionStatus.Cancelled => SessionLifecycle.Paused,
        _ => SessionLifecycle.Created,
    };

    private static string SurfaceLabel(SessionSurface surface) =>
        surface == SessionSurface.Cowork ? "cowork" : "code";

    private static string? EmailOf(ClaimsPrincipal user)
    {
        var email = user.FindFirst("email")?.Value;
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }
}

/// <summary>Selects the account store from the session store mode.</summary>
internal static class AccountStoreRegistration
{
    /// <summary>Registers the in-memory twin and the PostgreSQL store, and selects one from <c>Sessions:Store</c> at resolution time.</summary>
    /// <param name="services">The service collection.</param>
    public static void AddAccountStore(IServiceCollection services)
    {
        services.AddSingleton<InMemoryAccountStore>();
        services.AddSingleton<IAccountStore>(static provider =>
            SessionsOptions.IsPostgresStore(provider.GetRequiredService<IOptions<SessionsOptions>>().Value.Store)
                ? new PostgresAccountStore(provider.GetRequiredService<NpgsqlDataSource>())
                : provider.GetRequiredService<InMemoryAccountStore>());
    }
}
