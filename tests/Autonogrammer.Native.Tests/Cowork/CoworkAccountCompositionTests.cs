using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Projects;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>
/// The head's account data for Cowork sessions over the production composition. Only the account session and the HTTP transport are fakes: the API answers
/// with bearer checks, and every request the head sends is recorded so the tests can read its path, bearer and body.
/// </summary>
public sealed class CoworkAccountCompositionTests
{
    private const string ProjectId = "p-1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The production head reaches the signed-in account's project, global instructions and memory through its account context.</summary>
    [Fact]
    public async Task TheActualHeadSyncsAndRecallsWithTheSignedInAccountsBearer()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions()));
        var variables = ApiVariables();
        var services = await HeadServices.CreateAsync(HeadOptions(tree, session, api, variables), ct).ConfigureAwait(true);
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, new ScriptedOmp(), variables, services: services)).ConfigureAwait(true);
        await using (head.ConfigureAwait(false))
        {
            head.Cowork.ShouldNotBeNull();
            head.Cowork.AccountContext.ShouldBeSameAs(services.CoworkAccount);
            AgentHead.CoworkGate(services).IsSignedIn.ShouldBeTrue();
            AgentHead.CoworkGate(services).AccountFolderKey.ShouldBe(CoworkAccountFolder.KeyFromSubject("user-a"));
            new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert("s-1", new CoworkSessionMeta { ProjectId = ProjectId });

            var snapshot = await services.CoworkAccount.SnapshotAsync("s-1", ct).ConfigureAwait(true);

            snapshot.AccountKey.ShouldBe(CoworkAccountFolder.KeyFromSubject("user-a"));
            snapshot.SyncState.ShouldBe(ProjectSyncState.Synced);
            snapshot.IsReadOnly.ShouldBeFalse();
            snapshot.GlobalInstructions.ShouldBe("global-a");
            snapshot.ProjectId.ShouldBe(ProjectId);
            snapshot.ProjectState.ShouldBe(CoworkAccountSnapshot.ProjectActive);
            snapshot.ProjectInstructions.ShouldBe("project-a");
            snapshot.MemoryState.ShouldBe(MemoryCodes.Ok);
            snapshot.MemoryBrief.ShouldContain("memory-a");
            services.CoworkAccount.IsCurrent(snapshot).ShouldBeTrue();
            api.Requests.Count.ShouldBeGreaterThanOrEqualTo(4);
            api.Requests.ShouldAllBe(static request => request.Bearer == "Bearer token-a");
            api.Requests.Select(static request => request.Route).ShouldContain("GET /cowork/projects", StringComparer.Ordinal);
            api.Requests.Select(static request => request.Route).ShouldContain("GET /cowork/instructions/global", StringComparer.Ordinal);
            api.Requests.Select(static request => request.Route).ShouldContain("POST /cowork/memory/recall", StringComparer.Ordinal);
            (api.Requests.Single(static request => string.Equals(request.Route, "POST /cowork/memory/recall", StringComparison.Ordinal)).Body ?? string.Empty).ShouldContain("\"projectId\":\"p-1\"");
            api.Requests.Where(static request => request.Body is not null).ShouldAllBe(static request => !request.Body!.Contains("folder", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>After an account switch the old account's snapshot is not current, and the new account sees only its own data and bearer.</summary>
    [Fact]
    public async Task AnAccountSwitchNeverDeliversTheOldAccountsDataOrBearer()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions()));
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);
        new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert("s-1", new CoworkSessionMeta { ProjectId = ProjectId });
        var before = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);
        before.GlobalInstructions.ShouldBe("global-a");

        session.SignIn("user-b", "token-b");
        var requestsBeforeB = api.Requests.Count;
        var after = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        context.IsCurrent(before).ShouldBeFalse();
        after.AccountKey.ShouldBe(CoworkAccountFolder.KeyFromSubject("user-b"));
        after.GlobalInstructions.ShouldBe("global-b");
        after.ProjectId.ShouldBeNull("account B has no project link for session s-1");
        after.ProjectInstructions.ShouldBeNull();
        after.MemoryBrief.ShouldNotContain("memory-a");
        after.MemoryBrief.ShouldContain("memory-b");
        api.Requests.Skip(requestsBeforeB).ShouldAllBe(static request => request.Bearer == "Bearer token-b");
    }

    /// <summary>A rotated token for the same account is read by the next request, and the account graph is kept.</summary>
    [Fact]
    public async Task ARotatedTokenIsReadOnTheNextRequestAndTheAccountGraphIsKept()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions()));
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);
        _ = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);
        var key = context.AccountKey;

        session.SignIn("user-a", "token-a-rotated");
        var requestsBefore = api.Requests.Count;
        var snapshot = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        context.AccountKey.ShouldBe(key);
        snapshot.GlobalInstructions.ShouldBe("global-a");
        api.Requests.Skip(requestsBefore).ShouldAllBe(static request => request.Bearer == "Bearer token-a-rotated");
    }

    /// <summary>Signing out refuses every request: the context returns no account data and sends nothing.</summary>
    [Fact]
    public async Task SignOutRefusesTheSnapshotAndSendsNoRequest()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions()));
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);
        (await context.SnapshotAsync("s-1", ct).ConfigureAwait(true)).GlobalInstructions.ShouldBe("global-a");
        var requests = api.Requests.Count;

        session.SignOut();
        var signedOut = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        context.AccountKey.ShouldBeNull();
        signedOut.AccountKey.ShouldBeNull();
        signedOut.SyncState.ShouldBe(ProjectSyncState.SignedOut);
        signedOut.GlobalInstructions.ShouldBeNull();
        signedOut.MemoryBrief.ShouldBeEmpty();
        api.Requests.Count.ShouldBe(requests);
    }

    /// <summary>A sync in flight when the account switches is cancelled; its late completion returns nothing from the old account.</summary>
    [Fact]
    public async Task AnInFlightSnapshotIsCancelledBySwitchAndReturnsNothingFromTheOldAccount()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions())) { Block = true };
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);
        var inFlight = context.SnapshotAsync("s-1", ct);
        await api.WaitForFirstRequestAsync(ct).ConfigureAwait(true);

        session.SignIn("user-b", "token-b");
        var late = await inFlight.ConfigureAwait(true);

        late.AccountKey.ShouldBeNull("the old account's late completion must not name an account");
        late.GlobalInstructions.ShouldBeNull();
        late.ProjectInstructions.ShouldBeNull();
        late.MemoryBrief.ShouldBeEmpty();
        api.Block = false;
        var current = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);
        current.GlobalInstructions.ShouldBe("global-b");
    }

    /// <summary>A start while the API is unreachable serves the same account's last copy, read-only, from the account folder.</summary>
    [Fact]
    public async Task AnOfflineStartServesTheSameAccountsLastCopyReadOnly()
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert("s-1", new CoworkSessionMeta { ProjectId = ProjectId });
        using (var online = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions())))
        using (var client = new HttpClient(online) { BaseAddress = new Uri("https://api.test") })
        using (var first = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client))
        {
            (await first.SnapshotAsync("s-1", ct).ConfigureAwait(true)).ProjectInstructions.ShouldBe("project-a");
        }

        using var offline = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions())) { Offline = true };
        using var offlineClient = new HttpClient(offline) { BaseAddress = new Uri("https://api.test") };
        using var restarted = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, offlineClient);
        var snapshot = await restarted.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        snapshot.AccountKey.ShouldBe(CoworkAccountFolder.KeyFromSubject("user-a"));
        snapshot.SyncState.ShouldBe(ProjectSyncState.Offline);
        snapshot.IsReadOnly.ShouldBeTrue();
        snapshot.GlobalInstructions.ShouldBe("global-a");
        snapshot.ProjectInstructions.ShouldBe("project-a");
        snapshot.ProjectState.ShouldBe(CoworkAccountSnapshot.ProjectActive);
        snapshot.MemoryBrief.ShouldBeEmpty();
    }

    /// <summary>A 401 or a 429 keeps the same account's copy, read-only, with the state of the response.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProjectSyncState.TokenExpired)]
    [InlineData((HttpStatusCode)429, ProjectSyncState.RateLimited)]
    public async Task ARefusedSyncKeepsTheLastCopyWithItsDegradedState(HttpStatusCode status, string expectedState)
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using (var online = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions())))
        using (var client = new HttpClient(online) { BaseAddress = new Uri("https://api.test") })
        using (var first = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client))
        {
            _ = await first.SnapshotAsync("s-1", ct).ConfigureAwait(true);
        }

        using var refused = new ScriptedApi(static (_, _) => (HttpStatusCode.OK, "{}")) { Refuse = status };
        using var refusedClient = new HttpClient(refused) { BaseAddress = new Uri("https://api.test") };
        using var restarted = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, refusedClient);
        var snapshot = await restarted.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        snapshot.SyncState.ShouldBe(expectedState);
        snapshot.IsReadOnly.ShouldBeTrue();
        snapshot.GlobalInstructions.ShouldBe("global-a");
    }

    /// <summary>A session whose memory is off for the session, or an account whose memory is disabled or paused, sends no recall.</summary>
    [Theory]
    [InlineData(true, true, false, MemoryCodes.MemoryOffForTask)]
    [InlineData(false, false, false, CoworkAccountSnapshot.MemoryDisabled)]
    [InlineData(false, true, true, MemoryCodes.MemoryPaused)]
    public async Task MemoryIsNotRecalledWhenTheSessionOrAccountTurnsItOff(bool offForSession, bool enabled, bool paused, string expectedState)
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert("s-1", new CoworkSessionMeta { ProjectId = ProjectId, MemoryOff = offForSession });
        using var api = new ScriptedApi((bearer, route) => Respond(Tag(bearer), route, new RouteOptions { MemoryEnabled = enabled, MemoryPaused = paused }));
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);

        var snapshot = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        snapshot.MemoryState.ShouldBe(expectedState);
        snapshot.MemoryBrief.ShouldBeEmpty();
        api.Requests.Select(static request => request.Route).ShouldNotContain("POST /cowork/memory/recall", StringComparer.Ordinal);
    }

    /// <summary>A project session recalls with its project and its own account-memory flag; a session without a project recalls account entries.</summary>
    [Theory]
    [InlineData(false, ProjectId, false)]
    [InlineData(true, ProjectId, true)]
    [InlineData(false, null, true)]
    public async Task RecallUsesTheSessionsProjectAndAccountMemoryScope(bool projectUsesAccountMemory, string? linkedProject, bool expectedUseAccountMemory)
    {
        var ct = Token;
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert("s-1", new CoworkSessionMeta { ProjectId = linkedProject });
        using var api = new ScriptedApi((bearer, route) => Respond(Tag(bearer), route, new RouteOptions { ProjectUsesAccountMemory = projectUsesAccountMemory }));
        using var client = new HttpClient(api) { BaseAddress = new Uri("https://api.test") };
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, client);

        var snapshot = await context.SnapshotAsync("s-1", ct).ConfigureAwait(true);

        snapshot.MemoryState.ShouldBe(MemoryCodes.Ok);
        var recall = api.Requests.Single(static request => string.Equals(request.Route, "POST /cowork/memory/recall", StringComparison.Ordinal)).Body.ShouldNotBeNull();
        recall.ShouldContain("\"useAccountMemory\":" + (expectedUseAccountMemory ? "true" : "false"));
        if (linkedProject is null)
        {
            recall.ShouldNotContain("projectId");
        }
        else
        {
            recall.ShouldContain("\"projectId\":\"" + linkedProject + "\"");
        }
    }

    /// <summary>
    /// The first send of a project-linked task carries the account's standing block as the first embedded resource, byte-exact with the stored snapshot,
    /// before the user text. The block is acknowledged by the send, so the next send does not repeat it.
    /// </summary>
    [Fact]
    public async Task TheFirstCoworkPromptCarriesTheStandingBlockBeforeTheUserText()
    {
        var ct = Token;
        await using var ui = TestAppBuilder.StartSession();
        await ui.RunAsync(() => FirstPromptBodyAsync(ct), ct).ConfigureAwait(true);
    }

    // The send path marshals onto the UI thread, so the body runs inside the UI session the test starts.
    private static async Task FirstPromptBodyAsync(CancellationToken ct)
    {
        using var tree = new TempTree();
        var session = new ControllableAccountSession();
        session.SignIn("user-a", "token-a");
        using var api = new ScriptedApi(static (bearer, route) => Respond(Tag(bearer), route, new RouteOptions()));
        var variables = ApiVariables();
        var services = await HeadServices.CreateAsync(HeadOptions(tree, session, api, variables), ct).ConfigureAwait(true);
        var omp = new ScriptedOmp();
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, omp, variables, services: services)).ConfigureAwait(true);
        await using (head.ConfigureAwait(false))
        {
            var cowork = head.Cowork.ShouldNotBeNull();
            var accountKey = CoworkAccountFolder.KeyFromSubject("user-a");
            var id = await cowork.Sessions.CreateSessionAsync(ct).ConfigureAwait(true);
            new CoworkSessionMetaStore(tree.State, accountKey).Upsert(id, new CoworkSessionMeta { ProjectId = ProjectId });
            await cowork.Sessions.OpenSessionAsync(Row(id), ct).ConfigureAwait(true);

            await cowork.Sessions.SendAsync(id, "hello cowork", null, ct).ConfigureAwait(true);
            var first = (await PromptsAsync(omp, 1, ct).ConfigureAwait(true))[0].AsObject();
            var blocks = first["params"]!["prompt"]!.AsArray();
            var stored = new CoworkFirstPromptStore(tree.State, accountKey).Read(id).ShouldNotBeNull();
            blocks[0]!["type"]!.GetValue<string>().ShouldBe("resource");
            blocks[0]!["resource"]!["uri"]!.GetValue<string>().ShouldBe(CoworkFirstPromptStore.ResourceUriFor(id).AbsoluteUri);
            blocks[0]!["resource"]!["text"]!.GetValue<string>().ShouldBe(stored.Text);
            stored.Text.ShouldContain("global-a", (Case)StringComparison.Ordinal);
            stored.Text.ShouldContain("project-a", (Case)StringComparison.Ordinal);
            stored.Text.ShouldContain("memory-a", (Case)StringComparison.Ordinal);
            blocks[1]!["type"]!.GetValue<string>().ShouldBe("text");
            blocks[1]!["text"]!.GetValue<string>().ShouldBe("hello cowork");
            await WaitForStateAsync(tree.State, accountKey, id, CoworkFirstPromptState.Acknowledged, ct).ConfigureAwait(true);

            await cowork.Sessions.SendAsync(id, "second words", null, ct).ConfigureAwait(true);
            var second = (await PromptsAsync(omp, 2, ct).ConfigureAwait(true))[1].AsObject();
            var secondBlocks = second["params"]!["prompt"]!.AsArray();
            secondBlocks.Count.ShouldBe(1);
            secondBlocks[0]!["type"]!.GetValue<string>().ShouldBe("text");
            secondBlocks[0]!["text"]!.GetValue<string>().ShouldBe("second words");
        }
    }

    private static async Task<List<JsonNode>> PromptsAsync(ScriptedOmp omp, int count, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var requests = omp.Requests("session/prompt");
            if (requests.Count >= count)
            {
                return [.. requests.Take(count)];
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException($"The fake omp never received prompt {count}.");
    }

    private static async Task WaitForStateAsync(string stateRoot, string accountKey, string id, CoworkFirstPromptState state, CancellationToken cancellationToken)
    {
        var store = new CoworkFirstPromptStore(stateRoot, accountKey);
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (store.Read(id)?.State == state)
            {
                return;
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(true);
        }

        throw new TimeoutException($"The first-prompt record never reached {state}.");
    }

    private static AgentRow Row(string id) => new()
    {
        Id = id,
        Title = "task",
        Summary = new SessionSummary { Id = id, Title = "task", Lifecycle = SessionLifecycle.Created, CreatedAt = DateTimeOffset.UnixEpoch, LastActivity = DateTimeOffset.UnixEpoch },
        Status = AgentRowStatus.Ready,
        Group = AgentRowGroup.Idle,
        StatusText = "Ready",
        IconKey = "ready",
        LastActivityText = "now",
        IsLongRunning = false,
        IsStale = false,
        IsLost = false,
        AccessibleName = "task",
    };

    private static HeadServicesOptions HeadOptions(TempTree tree, IAccountSession session, HttpMessageHandler api, Func<string, string?> variables) => new()
    {
        StateRoot = tree.State,
        PluginsRoot = tree.Plugins,
        WorkspaceRoot = tree.Workspace,
        HomeDirectory = Path.Combine(tree.Root, "home"),
        ConfigDirectory = Path.Combine(tree.Root, "config"),
        GetVariable = variables,
        ApiHandler = api,
        AccountSession = session,
    };

    private static Func<string, string?> ApiVariables() =>
        name => string.Equals(name, HeadServices.ApiBaseUrlVariable, StringComparison.Ordinal) ? "https://api.test" : null;

    private static string Tag(string? bearer) => string.Equals(bearer, "Bearer token-b", StringComparison.Ordinal) ? "b" : "a";

    private static (HttpStatusCode Status, string Body) Respond(string tag, string route, RouteOptions options) => route switch
    {
        "GET /cowork/projects" => (HttpStatusCode.OK, "[{\"id\":\"" + ProjectId + "\",\"name\":\"Project " + tag + "\",\"instructions\":\"project-" + tag + "\",\"useAccountMemory\":" + Json(options.ProjectUsesAccountMemory) + ",\"contextUrls\":[]}]"),
        "GET /cowork/instructions/global" => (HttpStatusCode.OK, "{\"instructions\":\"global-" + tag + "\"}"),
        "GET /cowork/memory/settings" => (HttpStatusCode.OK, "{\"enabled\":" + Json(options.MemoryEnabled) + ",\"paused\":" + Json(options.MemoryPaused) + ",\"includeSensitive\":false}"),
        "POST /cowork/memory/recall" => (HttpStatusCode.OK, "{\"entries\":[{\"id\":\"m-" + tag + "\",\"projectId\":\"" + ProjectId + "\",\"topic\":\"Pref\",\"content\":\"memory-" + tag + "\",\"createdAt\":\"2026-11-01T00:00:00Z\",\"updatedAt\":\"2026-11-01T00:00:00Z\"}]}"),
        _ => (HttpStatusCode.NotFound, "{}"),
    };

    private static string Json(bool value) => value ? "true" : "false";

    /// <summary>What the scripted API serves for the account's project and memory.</summary>
    private sealed record RouteOptions
    {
        public bool ProjectUsesAccountMemory { get; init; }

        public bool MemoryEnabled { get; init; } = true;

        public bool MemoryPaused { get; init; }
    }

    /// <summary>One request the head sent: its route, the bearer it carried and its body, if any.</summary>
    private sealed record ApiRequest(string Route, string? Bearer, string? Body);

    /// <summary>A transport that checks each request's bearer and answers from a script. Requests are recorded, never logged.</summary>
    private sealed class ScriptedApi(Func<string?, string, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the requests in the order they were sent.</summary>
        public List<ApiRequest> Requests { get; } = [];

        /// <summary>Waits until the first request arrives or the token is cancelled.</summary>
        public Task WaitForFirstRequestAsync(CancellationToken ct) => _firstRequest.Task.WaitAsync(ct);

        /// <summary>Gets or sets whether the API is unreachable.</summary>
        public bool Offline { get; set; }

        /// <summary>Gets or sets whether requests wait until they are cancelled.</summary>
        public bool Block { get; set; }

        /// <summary>Gets or sets a status that answers every request in place of the script.</summary>
        public HttpStatusCode? Refuse { get; set; }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var route = request.Method.Method + " " + request.RequestUri!.AbsolutePath;
            var bearer = request.Headers.Authorization is { } authorization ? authorization.Scheme + " " + authorization.Parameter : null;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new ApiRequest(route, bearer, body));
            _ = _firstRequest.TrySetResult();
            if (Offline)
            {
                throw new HttpRequestException("The API is unreachable.");
            }

            if (Block)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            var (status, text) = Refuse is { } refused ? (refused, "") : respond(bearer, route);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(text, Encoding.UTF8, "application/json"),
            };
            return response;
        }
    }

    /// <summary>An account session the test signs in and out. Its state changes raise the same event the desktop session raises.</summary>
    private sealed class ControllableAccountSession : IAccountSession
    {
        private string? _token;

        /// <inheritdoc />
        public event EventHandler<AccountStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public AccountState State { get; private set; } = AccountState.SignedOut;

        /// <summary>Signs the account in with a token and raises the state change.</summary>
        /// <param name="userId">The account subject.</param>
        /// <param name="token">The access token the API must receive.</param>
        public void SignIn(string userId, string token)
        {
            _token = token;
            Change(AccountState.SignedOut with { Phase = AuthPhase.SignedIn, Identity = new AccountIdentity(userId, userId + "@example.test") });
        }

        /// <summary>Signs the account out and raises the state change.</summary>
        public void SignOut()
        {
            _token = null;
            Change(AccountState.SignedOut);
        }

        /// <inheritdoc />
        public ValueTask<AuthResult<SecretString>> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            var token = _token;
            return ValueTask.FromResult(token is null
                ? AuthResult.Fail<SecretString>(AuthErrorKind.SessionEnded)
                : AuthResult.Success(new SecretString(token)));
        }

        /// <inheritdoc />
        public Task<AuthResult<AuthUnit>> SignOutAsync(SignOutScope scope, CancellationToken cancellationToken)
        {
            _ = scope;
            _ = cancellationToken;
            SignOut();
            return Task.FromResult(AuthResult.Success(AuthUnit.Value));
        }

        private void Change(AccountState state)
        {
            State = state;
            StateChanged?.Invoke(this, new AccountStateChangedEventArgs(state));
        }
    }
}
