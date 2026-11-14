using System.Text;
using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Projects;
using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Secrets;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Native.Views.Cowork;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Instructions;
using Autonogrammer.Presentation.Cowork.Projects;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;
using CoworkProjectStore = Autonogrammer.Cowork.Projects.CoworkProjectStore;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>
/// Project selection to Cowork task creation: the rail's picker and new-task action reach the host, the host stores the project, the trusted folders and the
/// memory mode before the task is returned, refused selections create nothing, and sign-out or an account switch changes what the rail and the host see.
/// </summary>
public sealed class CoworkProjectSessionSelectionTests
{
    /// <summary>A linked task stores its project, the project folder and the device's grants, with the fixed memory mode, before it is returned.</summary>
    [Fact]
    public async Task ALinkedTaskStoresItsProjectFoldersAndModeBeforeItIsReturned()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws-a")).FullName;
        var extra = Directory.CreateDirectory(Path.Combine(tree.Root, "grant")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var key = CoworkAccountFolder.KeyFromSubject("user-a");
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-a", workspace));
        new CoworkFolderGrantStore(tree.State, key).Grant(new CoworkFolderGrant(extra, CoworkFolderAccess.ReadOnly, CoworkFolderScope.Global));
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);

        var (id, notice) = await host.CreateTaskAsync("p-a", ct).ConfigureAwait(true);

        notice.ShouldBeNull();
        var meta = new CoworkSessionMetaStore(tree.State, key).Get(id);
        meta.ShouldNotBeNull();
        meta.ProjectId.ShouldBe("p-a");
        meta.TrustedFolders.ShouldBe([workspace, extra]);
        meta.MemoryOff.ShouldBeFalse();
        host.TaskSummaries().Single(row => string.Equals(row.Id, id, StringComparison.Ordinal)).ProjectId.ShouldBe("p-a");
    }

    /// <summary>No project creates an unlinked task: no link is stored and the task lists with no project.</summary>
    [Fact]
    public async Task NoSelectionCreatesAnUnlinkedTask()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);

        var (id, notice) = await host.CreateTaskAsync(null, ct).ConfigureAwait(true);

        notice.ShouldBeNull();
        new CoworkSessionMetaStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Get(id).ShouldBeNull();
        host.TaskSummaries().Single(row => string.Equals(row.Id, id, StringComparison.Ordinal)).ProjectId.ShouldBeNull();
    }

    /// <summary>A project whose folder is not on this device is still linked: the notice is shown and no folder is invented.</summary>
    [Fact]
    public async Task AMissingProjectFolderIsReportedAndTheTaskIsStillCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var key = CoworkAccountFolder.KeyFromSubject("user-a");
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-a", Path.Combine(tree.Root, "gone")));
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);

        var (id, notice) = await host.CreateTaskAsync("p-a", ct).ConfigureAwait(true);

        notice.ShouldBe(CoworkDegradedSentences.FolderMissing);
        var meta = new CoworkSessionMetaStore(tree.State, key).Get(id);
        meta.ShouldNotBeNull();
        meta.ProjectId.ShouldBe("p-a");
        meta.TrustedFolders.ShouldBeEmpty();
    }

    /// <summary>An archived project, an unknown one and another account's project are refused, and no task is created for them.</summary>
    [Fact]
    public async Task AnArchivedUnknownOrOtherAccountsProjectIsRefusedAndNoTaskIsCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        new CoworkProjectStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert(Project("p-old", workspace, DateTimeOffset.UnixEpoch.AddDays(1)));
        new CoworkProjectStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-b")).Upsert(Project("p-b", workspace));
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);

        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync("p-old", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync("p-missing", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync("p-b", ct)).ConfigureAwait(true);

        host.TaskSummaries().ShouldBeEmpty();
    }

    /// <summary>Sign-out refuses creation and shows no task; an account switch shows only the new account's tasks and refuses the old account's project.</summary>
    [Fact]
    public async Task SignOutAndAccountSwitchNeverShowOrLinkAnotherAccountsTasks()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        new CoworkProjectStore(tree.State, CoworkAccountFolder.KeyFromSubject("user-a")).Upsert(Project("p-a", workspace));
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);
        var (idA, _) = await host.CreateTaskAsync("p-a", ct).ConfigureAwait(true);

        session.SignOut();
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync("p-a", ct)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync(null, ct)).ConfigureAwait(true);
        host.TaskSummaries().ShouldBeEmpty();

        session.SignIn("user-b", "token-b");
        host.TaskSummaries().ShouldBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => host.CreateTaskAsync("p-a", ct)).ConfigureAwait(true);

        session.SignIn("user-a", "token-a");
        var returned = host.TaskSummaries().Single(row => string.Equals(row.Id, idA, StringComparison.Ordinal));
        returned.ProjectId.ShouldBe("p-a");
    }

    /// <summary>A link that cannot be stored leaves the task marked Failed and reports the original error; the task never opens unlinked.</summary>
    [Fact]
    public async Task ALinkThatCannotBeStoredMarksTheTaskFailed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var key = CoworkAccountFolder.KeyFromSubject("user-a");
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-a", workspace));
        var meta = new CoworkSessionMetaStore(tree.State, key);
        await File.WriteAllTextAsync(meta.FilePath, new string('x', CoworkSessionMetaStore.MaxFileBytes + 1), Encoding.UTF8, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);

        await Should.ThrowAsync<InvalidDataException>(() => host.CreateTaskAsync("p-a", ct)).ConfigureAwait(true);

        var row = host.TaskSummaries().Single();
        row.Status.ShouldBe(CoworkSessionStatus.Failed);
        row.ProjectId.ShouldBeNull();
    }

    /// <summary>Resolving a reopened task reads the project its stored link names, not the project selected now.</summary>
    [Fact]
    public async Task ResolvingATaskReadsItsStoredProject()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var key = CoworkAccountFolder.KeyFromSubject("user-a");
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-a", workspace));
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-other", workspace));
        using var context = new CoworkAccountContext(new NativeCoworkAccountGate(session), session, tree.State, null);
        var host = Host(tree.State, session, context);
        await using var hostScope = host.ConfigureAwait(true);
        var (id, _) = await host.CreateTaskAsync("p-a", ct).ConfigureAwait(true);

        var snapshot = await host.ResolveTaskContextAsync(id, ct).ConfigureAwait(true);

        snapshot.ShouldNotBeNull();
        snapshot.AccountKey.ShouldBe(key);
        snapshot.ProjectId.ShouldBe("p-a");
        snapshot.ProjectState.ShouldBe(CoworkAccountSnapshot.ProjectActive);
        snapshot.SyncState.ShouldBe(ProjectSyncState.Offline);
    }

    /// <summary>The rail's picker and New task action create the linked task, select it, and "No project" creates an unlinked one.</summary>
    [Fact]
    public async Task TheRailPickerAndNewTaskActionReachTheHost()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var workspace = Directory.CreateDirectory(Path.Combine(tree.Root, "ws")).FullName;
        var session = new FakeSession();
        session.SignIn("user-a", "token-a");
        var key = CoworkAccountFolder.KeyFromSubject("user-a");
        new CoworkProjectStore(tree.State, key).Upsert(Project("p-a", workspace));
        var host = Host(tree.State, session, context: null);
        await using var hostScope = host.ConfigureAwait(true);
        var headless = TestAppBuilder.StartSession();
        await using var headlessScope = headless.ConfigureAwait(true);
        await headless.RunAsync(async () =>
        {
            var catalog = new HostCatalog(host);
            var list = new CoworkSessionListModel(catalog);
            var projects = new ProjectListModel(new FixedProjects(new ProjectListItem("p-a", "Project p-a", false, 1, 0)));
            await projects.LoadAsync(ct).ConfigureAwait(true);
            var rail = new CoworkRailView();
            rail.Attach(list);
            rail.BindTasks(projects, async (projectId, token) =>
            {
                var created = await host.CreateTaskAsync(projectId, token).ConfigureAwait(true);
                catalog.NotifyChanged();
                return created;
            });

            rail.ProjectChoice.SelectedItem = projects.Rows[0];
            projects.SelectedId.ShouldBe("p-a");

            var linked = await rail.NewTaskAsync(ct).ConfigureAwait(true);

            linked.ShouldNotBeNull();
            new CoworkSessionMetaStore(tree.State, key).Get(linked).ShouldNotBeNull().ProjectId.ShouldBe("p-a");
            list.SelectedId.ShouldBe(linked);

            rail.NoProject.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            projects.SelectedId.ShouldBeNull();

            var unlinked = await rail.NewTaskAsync(ct).ConfigureAwait(true);

            unlinked.ShouldNotBeNull();
            new CoworkSessionMetaStore(tree.State, key).Get(unlinked).ShouldBeNull();
        }, ct).ConfigureAwait(true);
    }

    private static CoworkSessionHost Host(string root, FakeSession session, CoworkAccountContext? context) =>
        new(new CoworkSessionHostOptions
        {
            StateRoot = root,
            WorkingDirectory = root,
            Gate = new NativeCoworkAccountGate(session),
            AccountContext = context,
        });

    private static Autonogrammer.Cowork.Projects.CoworkProject Project(string id, string? folder, DateTimeOffset? archivedAt = null) => new()
    {
        Id = id,
        Name = "Project " + id,
        Folders = folder is null ? [] : [folder],
        ArchivedAt = archivedAt,
    };

    private sealed class HostCatalog(CoworkSessionHost host) : ICoworkSessionCatalog
    {
        public event EventHandler? Changed;

        public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => host.TaskSummaries();
    }

    private sealed class FixedProjects(params ProjectListItem[] rows) : IProjectListSource
    {
        public Task<CoworkChannelResult<IReadOnlyList<ProjectListItem>>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CoworkChannelResult<IReadOnlyList<ProjectListItem>>(true, false, rows, ProjectSyncState.Synced, null));
    }

    /// <summary>An account session the test signs in and out. Its state changes raise the same event the desktop session raises.</summary>
    private sealed class FakeSession : IAccountSession
    {
        private string? _token;

        /// <inheritdoc />
        public event EventHandler<AccountStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public AccountState State { get; private set; } = AccountState.SignedOut;

        /// <summary>Signs the account in with a token and raises the state change.</summary>
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
