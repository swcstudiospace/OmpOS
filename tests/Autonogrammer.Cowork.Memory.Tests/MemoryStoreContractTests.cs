#pragma warning disable CA1034, CA2000, CA1001, CA2007, CA2100, MA0004, MA0006, MA0051, IDE0005, CS8604
using System.Net;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Memory;
using Autonogrammer.Cowork.Memory.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Tests;

/// <summary>Behaviours every memory store keeps: scope, caps, pause, reset, delete, export and policy.</summary>
public abstract class MemoryStoreContractTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates a store for one account.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="notices">The notice sink.</param>
    /// <returns>The store.</returns>
    protected abstract IMemoryStore Create(string accountId, IMemoryNoticeSink notices);

    /// <summary>Inserts a project row when the store has a foreign key.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="projectId">The project.</param>
    /// <returns>A task.</returns>
    protected virtual Task EnsureProjectAsync(string accountId, string projectId) => Task.CompletedTask;

    /// <summary>A project session sees project entries, and account entries only with useAccountMemory.</summary>
    [Fact]
    public async Task ScopeFollowsProjectAndAccount()
    {
        var store = Create("acct", new NoticeList());
        await EnsureProjectAsync("acct", "proj").ConfigureAwait(true);
        var project = new MemoryScope("acct", "proj", false, "sess");
        var account = new MemoryScope("acct", null, false, "sess");
        var withAccount = project with { UseAccountMemory = true };
        (await store.SaveAsync(project, new MemoryDraft("tea", "project oolong", null), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await store.SaveAsync(account, new MemoryDraft("tea", "account sencha", null), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var projectOnly = await store.RecallAsync(project, "tea", 10, Token).ConfigureAwait(true);
        projectOnly.Entries.Select(static entry => entry.Content).ShouldBe(["project oolong"]);

        var both = await store.RecallAsync(withAccount, "tea", 10, Token).ConfigureAwait(true);
        both.Entries.Select(static entry => entry.Content).ShouldBe(["project oolong", "account sencha"], ignoreOrder: true);

        var noProject = await store.RecallAsync(account, "tea", 10, Token).ConfigureAwait(true);
        noProject.Entries.Select(static entry => entry.Content).ShouldBe(["account sencha"]);
    }

    /// <summary>Pause keeps entries and refuses reads and writes. Reset hard-deletes, including project memory. One entry can be deleted. Export is JSON.</summary>
    [Fact]
    public async Task PauseResetDeleteAndExport()
    {
        var store = Create("acct", new NoticeList());
        await EnsureProjectAsync("acct", "proj").ConfigureAwait(true);
        var scope = new MemoryScope("acct", "proj", false, "sess");
        var saved = await store.SaveAsync(scope, new MemoryDraft("tea", "keep me", null), Token).ConfigureAwait(true);
        saved.Entry.ShouldNotBeNull();
        await store.SetSettingsAsync("acct", new MemorySettings(true, true, false), Token).ConfigureAwait(true);
        var paused = await store.SaveAsync(scope, new MemoryDraft("tea", "nope", null), Token).ConfigureAwait(true);
        paused.Code.ShouldBe(MemoryCodes.MemoryPaused);
        (await store.RecallAsync(scope, "tea", 5, Token).ConfigureAwait(true)).Code.ShouldBe(MemoryCodes.MemoryPaused);
        await store.SetSettingsAsync("acct", new MemorySettings(true, false, false), Token).ConfigureAwait(true);
        (await store.RecallAsync(scope, null, 5, Token).ConfigureAwait(true)).Entries.Count.ShouldBe(1);

        (await store.DeleteAsync(scope, saved.Entry.Id, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await store.RecallAsync(scope, null, 5, Token).ConfigureAwait(true)).Entries.ShouldBeEmpty();
        _ = await store.SaveAsync(scope, new MemoryDraft("tea", "again", null), Token).ConfigureAwait(true);
        (await store.ResetAsync("acct", Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await store.RecallAsync(scope, null, 5, Token).ConfigureAwait(true)).Entries.ShouldBeEmpty();

        var exported = await store.SaveAsync(new MemoryScope("acct", null, false, "sess"), new MemoryDraft("tea", "export me", null), Token).ConfigureAwait(true);
        var json = await store.ExportAsync("acct", Token).ConfigureAwait(true);
        json.Succeeded.ShouldBeTrue();
        json.Json.ShouldNotBeNull();
        json.Json.ShouldContain("export me");
        json.Json.ShouldNotContain("\"accountId\"");
        exported.Entry!.ToString().ShouldNotContain("export me");
    }

    /// <summary>Policy runs before the write, and the caps are enforced.</summary>
    [Fact]
    public async Task PolicyAndCapsRefuseBeforeWrite()
    {
        var notices = new NoticeList();
        var store = Create("acct", notices);
        var scope = new MemoryScope("acct", null, false, "sess");
        var refused = await store.SaveAsync(scope, new MemoryDraft("id", "SSN 219-09-9999", null), Token).ConfigureAwait(true);
        refused.Succeeded.ShouldBeFalse();
        refused.Code.ShouldBe(MemoryCodes.MemoryRefusedNeverSave);
        refused.Sentence.ShouldNotContain("219-09-9999");
        notices.Items.ShouldContain(notice => notice.Code == MemoryCodes.MemoryRefusedNeverSave);
        (await store.RecallAsync(scope, null, 5, Token).ConfigureAwait(true)).Entries.ShouldBeEmpty();

        for (var i = 0; i < MemoryOptions.MaxSavesPerSessionPerHour; i++)
        {
            (await store.SaveAsync(scope, new MemoryDraft("n", "note " + i.ToString(System.Globalization.CultureInfo.InvariantCulture), null), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        }

        (await store.SaveAsync(scope, new MemoryDraft("n", "one more", null), Token).ConfigureAwait(true)).Code.ShouldBe(MemoryCodes.MemoryRateLimited);
    }

    /// <summary>Another account cannot see or delete these entries.</summary>
    [Fact]
    public async Task AccountsAreIsolated()
    {
        var left = Create("left", new NoticeList());
        var right = Create("right", new NoticeList());
        var saved = await left.SaveAsync(new MemoryScope("left", null, false, "sess"), new MemoryDraft("tea", "left only", null), Token).ConfigureAwait(true);
        (await right.RecallAsync(new MemoryScope("right", null, false, "sess"), "tea", 5, Token).ConfigureAwait(true)).Entries.ShouldBeEmpty();
        (await right.DeleteAsync(new MemoryScope("right", null, false, "sess"), saved.Entry!.Id, Token).ConfigureAwait(true)).Code.ShouldBe(MemoryCodes.MemoryNotFound);
        (await left.RecallAsync(new MemoryScope("left", null, false, "sess"), null, 5, Token).ConfigureAwait(true)).Entries.Count.ShouldBe(1);
    }

    /// <summary>Runs the contract against <see cref="InMemoryMemoryStore"/>.</summary>
    public sealed class InMemoryMemoryStoreContractTests : MemoryStoreContractTests
    {
        /// <inheritdoc />
        protected override IMemoryStore Create(string accountId, IMemoryNoticeSink notices) => new InMemoryMemoryStore(notices: notices);
    }

    /// <summary>Runs the contract against <see cref="HttpMemoryStore"/>.</summary>
    public sealed class HttpMemoryStoreContractTests : MemoryStoreContractTests
    {
        private readonly MemoryHttpHandler _handler = new();

        /// <inheritdoc />
        protected override IMemoryStore Create(string accountId, IMemoryNoticeSink notices)
        {
            var http = new HttpClient(_handler) { BaseAddress = new Uri("http://memory.test") };
            return new HttpMemoryStore(http, _ => ValueTask.FromResult<string?>(accountId), notices);
        }
    }

    /// <summary>Runs the contract against <see cref="PostgresMemoryStore"/> when the test database is up.</summary>
    [Trait("Category", "Live")]
    public sealed class PostgresMemoryStoreContractTests : MemoryStoreContractTests, IAsyncLifetime
    {
        private readonly string _schema = "t_" + Guid.NewGuid().ToString("n");
        private NpgsqlDataSource? _admin;
        private NpgsqlDataSource? _app;

        /// <inheritdoc />
        public async ValueTask InitializeAsync()
        {
            var configured = Environment.GetEnvironmentVariable("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION");
            if (string.IsNullOrWhiteSpace(configured))
            {
                throw new InvalidOperationException("AUTONOGRAMMER_TEST_POSTGRES_CONNECTION is unset. Source scripts/test-services.sh up before these tests.");
            }

            _admin = NpgsqlDataSource.Create(configured);
            await new MigrationRunner(_admin, _schema).MigrateAsync(CancellationToken.None).ConfigureAwait(true);
            var builder = new NpgsqlConnectionStringBuilder(configured) { SearchPath = _schema + ",public" };
            _app = NpgsqlDataSource.Create(builder.ConnectionString);
            foreach (var account in new[] { "acct", "left", "right" })
            {
                using var provision = _app.CreateCommand("INSERT INTO account_lifecycle (account_id) VALUES (@id) ON CONFLICT (account_id) DO NOTHING");
                provision.Parameters.AddWithValue("id", account);
                _ = await provision.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_app is not null)
            {
                await _app.DisposeAsync().ConfigureAwait(true);
            }

            if (_admin is null)
            {
                return;
            }

            await using var connection = await _admin.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(true);
            await using var drop = new NpgsqlCommand("DROP SCHEMA IF EXISTS " + _schema + " CASCADE", connection);
            _ = await drop.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(true);
            await _admin.DisposeAsync().ConfigureAwait(true);
        }

        /// <inheritdoc />
        protected override IMemoryStore Create(string accountId, IMemoryNoticeSink notices)
        {
            _ = accountId;
            return new PostgresMemoryStore(_app!, notices: notices);
        }

        /// <inheritdoc />
        protected override async Task EnsureProjectAsync(string accountId, string projectId)
        {
            await using var command = _app!.CreateCommand(
                """
                INSERT INTO cowork_projects (id, account_id, name, created_at, updated_at)
                VALUES (@id, @account_id, @name, now(), now())
                ON CONFLICT (id) DO NOTHING
                """);
            command.Parameters.AddWithValue("id", projectId);
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("name", "p");
            _ = await command.ExecuteNonQueryAsync(Token).ConfigureAwait(true);
        }
    }

    private sealed class NoticeList : IMemoryNoticeSink
    {
        public List<MemoryNotice> Items { get; } = [];

        public void Report(MemoryNotice notice) => Items.Add(notice);
    }

    /// <summary>A loopback that serves <c>/cowork/memory</c> from one in-memory store per bearer.</summary>
    private sealed class MemoryHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, InMemoryMemoryStore> _stores = new(StringComparer.Ordinal);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter;
            if (string.IsNullOrWhiteSpace(token))
            {
                return Json(HttpStatusCode.Unauthorized, new MemoryErrorRecord { Code = MemoryCodes.SignedOut, Sentence = "Signed out." });
            }

            if (!_stores.TryGetValue(token, out var store))
            {
                store = new InMemoryMemoryStore();
                _stores[token] = store;
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (path == HttpMemoryStore.SettingsPath && request.Method == HttpMethod.Get)
            {
                var settings = await store.GetSettingsAsync(token, cancellationToken).ConfigureAwait(false);
                return Json(HttpStatusCode.OK, new MemorySettingsRecord { Enabled = settings.Enabled, Paused = settings.Paused, IncludeSensitive = settings.IncludeSensitive });
            }

            if (path == HttpMemoryStore.SettingsPath && request.Method == HttpMethod.Put)
            {
                var record = JsonSerializer.Deserialize(body!, MemoryJsonContext.Default.MemorySettingsRecord)!;
                await store.SetSettingsAsync(token, new MemorySettings(record.Enabled, record.Paused, record.IncludeSensitive), cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path == HttpMemoryStore.CollectionPath && request.Method == HttpMethod.Post)
            {
                var save = JsonSerializer.Deserialize(body!, MemoryJsonContext.Default.MemorySaveRequest)!;
                var mutation = await store.SaveAsync(new MemoryScope(token, save.ProjectId, save.UseAccountMemory, save.SourceSession ?? "sess"), new MemoryDraft(save.Topic, save.Content, save.Category), cancellationToken).ConfigureAwait(false);
                return mutation.Succeeded
                    ? Json(HttpStatusCode.OK, MemorySearch.ToRecord(mutation.Entry!))
                    : Json(HttpStatusCode.BadRequest, new MemoryErrorRecord { Code = mutation.Code, Sentence = mutation.Sentence ?? mutation.Code });
            }

            if (path == HttpMemoryStore.RecallPath)
            {
                var recallRequest = JsonSerializer.Deserialize(body!, MemoryJsonContext.Default.MemoryRecallRequest)!;
                var recall = await store.RecallAsync(new MemoryScope(token, recallRequest.ProjectId, recallRequest.UseAccountMemory, "sess"), recallRequest.Query, recallRequest.K, cancellationToken).ConfigureAwait(false);
                return recall.Succeeded
                    ? Json(HttpStatusCode.OK, new MemoryRecallResponse { Entries = recall.Entries.Select(MemorySearch.ToRecord).ToArray() })
                    : Json(HttpStatusCode.Conflict, new MemoryErrorRecord { Code = recall.Code, Sentence = recall.Sentence ?? recall.Code });
            }

            if (path == HttpMemoryStore.ResetPath)
            {
                _ = await store.ResetAsync(token, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (path == HttpMemoryStore.ExportPath)
            {
                var export = await store.ExportAsync(token, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(export.Json ?? "{}", Encoding.UTF8, "application/json") };
            }

            if (request.Method == HttpMethod.Delete && path.StartsWith(HttpMemoryStore.CollectionPath + "/", StringComparison.Ordinal))
            {
                var id = Uri.UnescapeDataString(path[(HttpMemoryStore.CollectionPath.Length + 1)..]);
                var query = request.RequestUri?.Query ?? string.Empty;
                var project = Query(query, "projectId");
                var useAccount = Query(query, "useAccountMemory") == "true";
                var deleted = await store.DeleteAsync(new MemoryScope(token, string.IsNullOrEmpty(project) ? null : project, useAccount, "sess"), id, cancellationToken).ConfigureAwait(false);
                return deleted.Succeeded
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : Json(HttpStatusCode.NotFound, new MemoryErrorRecord { Code = deleted.Code, Sentence = deleted.Sentence ?? deleted.Code });
            }

            return Json(HttpStatusCode.NotFound, new MemoryErrorRecord { Code = MemoryCodes.Offline, Sentence = "Unknown route." });
        }

        private static string? Query(string query, string name)
        {
            var trimmed = query.TrimStart('?');
            foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 && pair[0] == name)
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return null;
        }

        private static HttpResponseMessage Json<T>(HttpStatusCode status, T value)
        {
            var json = JsonSerializer.Serialize(value, typeof(T), MemoryJsonContext.Default);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
