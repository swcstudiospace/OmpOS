using System.Net;
using System.Text;
using Autonogrammer.Cowork.Projects;
using SessionMetaStore = Autonogrammer.Cowork.Sessions.CoworkSessionMetaStore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Projects;

/// <summary>Sync, offline read-only, 401 and 429. Request bodies never contain folders.</summary>
public sealed class ProjectAccountSyncTests
{
    [Fact]
    public async Task SyncMergesMetadataAndNeverSendsFolders()
    {
        using var handler = new ScriptedHandler();
        handler.Enqueue(HttpStatusCode.OK, """[{"id":"p-1","name":"Remote","instructions":"from-api"}]""");
        handler.Enqueue(HttpStatusCode.OK, """{"instructions":"global-note"}""");
        handler.Enqueue(HttpStatusCode.OK, """{"id":"p-1","name":"Remote"}""");
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("sync"));
        store.Upsert(new CoworkProject { Id = "p-1", Name = "Local", Folders = ["/secret/folder"] });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var sync = new ProjectAccountSync(new HttpProjectApi(http, static () => "token"), store);

        (await sync.SyncAsync(CancellationToken.None)).ShouldBe(ProjectSyncState.Synced);
        sync.IsReadOnly.ShouldBeFalse();
        sync.GlobalInstructions.ShouldBe("global-note");
        store.List().ShouldHaveSingleItem().Folders.ShouldBe(["/secret/folder"]);

        var pushed = await sync.PushAsync(store.List().ShouldHaveSingleItem(), CancellationToken.None);
        pushed.Succeeded.ShouldBeTrue();
        var bodies = string.Join('\n', handler.Bodies);
        bodies.Contains("folders", StringComparison.Ordinal).ShouldBeFalse();
        bodies.Contains("/secret/folder", StringComparison.Ordinal).ShouldBeFalse();
        handler.Paths.Contains("/cowork/projects", StringComparer.Ordinal).ShouldBeTrue();
        handler.Paths.Contains("/cowork/instructions/global", StringComparer.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public async Task OfflineServesTheLastCopyReadOnly()
    {
        using var handler = new ScriptedHandler { Throw = true };
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("offline"));
        store.Upsert(new CoworkProject { Id = "p-1", Name = "Cached", Folders = ["/local"] });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var sync = new ProjectAccountSync(new HttpProjectApi(http, static () => "token"), store);

        (await sync.SyncAsync(CancellationToken.None)).ShouldBe(ProjectSyncState.Offline);
        sync.IsReadOnly.ShouldBeTrue();
        sync.ListLocal().ShouldHaveSingleItem().Name.ShouldBe("Cached");
        (await sync.PushAsync(sync.ListLocal()[0], CancellationToken.None)).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task UnauthorizedIsTokenExpiredAndReadOnly()
    {
        using var handler = new ScriptedHandler();
        handler.Enqueue(HttpStatusCode.Unauthorized, "");
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("expired"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var sync = new ProjectAccountSync(new HttpProjectApi(http, static () => "token"), store);

        (await sync.SyncAsync(CancellationToken.None)).ShouldBe(ProjectSyncState.TokenExpired);
        sync.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task TooManyRequestsIsRateLimited()
    {
        using var handler = new ScriptedHandler();
        handler.Enqueue((HttpStatusCode)429, "");
        using var directory = new TempState();
        var store = new CoworkProjectStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("limited"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var sync = new ProjectAccountSync(new HttpProjectApi(http, static () => "token"), store);

        (await sync.SyncAsync(CancellationToken.None)).ShouldBe(ProjectSyncState.RateLimited);
        sync.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task GlobalInstructionsOverEightKibAreRefusedBeforeSend()
    {
        using var handler = new ScriptedHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var api = new HttpProjectApi(http, static () => "token");
        var over = new string('g', (8 * 1024) + 1);
        var result = await api.PutGlobalInstructionsAsync(over, CancellationToken.None);
        result.Succeeded.ShouldBeFalse();
        handler.Bodies.ShouldBeEmpty();
    }

    [Fact]
    public async Task ABodyNamingAnAccountIsRefusedBeforeSend()
    {
        HttpProjectApi.BodyNamesAccount("""{"account":"other"}""").ShouldBeTrue();
        using var handler = new ScriptedHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
        var api = new HttpProjectApi(http, static () => "token");
        var refused = await api.UpsertAsync(new Autonogrammer.Contracts.Cowork.Projects.ProjectSyncRecord { Id = "p", Name = "n" }, CancellationToken.None);
        refused.Succeeded.ShouldBeTrue();
        handler.Bodies.ShouldAllBe(static body => !body.Contains("folders", StringComparison.Ordinal));
    }

    [Fact]
    public void SessionMetaLinksAProjectAndDoesNotGiveCodeStandingContext()
    {
        using var directory = new TempState();
        var store = new SessionMetaStore(directory.Path, global::Autonogrammer.Cowork.CoworkAccountFolder.KeyFromSubject("meta"));
        store.Upsert("task-1", new Autonogrammer.Cowork.Sessions.CoworkSessionMeta { ProjectId = "p-1", MemoryOff = true });
        var meta = store.Get("task-1");
        meta.ShouldNotBeNull();
        meta.ProjectId.ShouldBe("p-1");
        meta.MemoryOff.ShouldBeTrue();
        SessionMetaStore.CodeSessionsReceiveStandingContext.ShouldBeFalse();
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<string> Bodies { get; } = [];

        public List<string> Paths { get; } = [];

        public bool Throw { get; set; }

        public void Enqueue(HttpStatusCode status, string body)
        {
#pragma warning disable CA2000 // Owned by the queue and disposed with the handler.
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            _responses.Enqueue(response);
#pragma warning restore CA2000
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new HttpRequestException("offline");
            }

            Paths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }

            return _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                while (_responses.Count > 0)
                {
                    _responses.Dequeue().Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TempState : IDisposable
    {
        public TempState()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cowork-sync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
