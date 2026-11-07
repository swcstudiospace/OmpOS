using System.Text.Json;
using Autonogrammer.Accounts.Desktop.Tests.Support;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Accounts.Desktop.Tests;

/// <summary>The cross-process file lock as the <see cref="IRefreshGuard"/> of two managers over one store and one lock file (AG-67-6, D-20, D-63).</summary>
public sealed class FileLockAdoptionTests : IDisposable
{
    private const string Email = "person@example.com";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ag-67-04-" + Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task TwoManagersRacingARefreshEndWithOneSpendingTheTokenAndTheOtherAdoptingTheRotatedOne()
    {
        var time = new ManualTimeProvider();
        var host = new FakeGoTrueHost(time) { ReuseInterval = TimeSpan.Zero };
        await using (host.ConfigureAwait(true))
        {
            var store = new InMemorySessionSecretStore();
            string lockFile = Path.Combine(_directory, "account", "refresh.lock");
            GoTrueSession seed = host.IssueSession(Email);
            using (Seat seeder = new(host, store, time, guard: null))
            {
                await seeder.Manager.SignInAsync(seed, host.Options.Issuer, Token).ConfigureAwait(true);
            }

            using Seat first = new(host, store, time, new CrossProcessFileLock(lockFile));
            var guardOfSecond = new ObservedGuard(new CrossProcessFileLock(lockFile));
            using Seat second = new(host, store, time, guardOfSecond);
            host.HoldRefresh();

            await first.Manager.StartAsync(Token).ConfigureAwait(true);
            await UntilAsync(() => host.Count("token:refresh") == 1).ConfigureAwait(true);
            await second.Manager.StartAsync(Token).ConfigureAwait(true);
            await guardOfSecond.Requested.Task.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            await Task.Delay(100, Token).ConfigureAwait(true);
            host.Count("token:refresh").ShouldBe(1);

            time.Advance(TimeSpan.FromSeconds(1));
            host.ReleaseRefresh();
            await UntilAsync(() => first.Manager.State.Connectivity == AccountConnectivity.Online && second.Manager.State.Connectivity == AccountConnectivity.Online).ConfigureAwait(true);

            IReadOnlyList<string> presented = host.PresentedRefreshTokens;
            presented.Count.ShouldBe(2);
            presented[0].ShouldBe(seed.RefreshToken.Reveal());
            string.Equals(presented[1], presented[0], StringComparison.Ordinal).ShouldBeFalse();
            first.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
            second.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
            first.Manager.State.Notices.ShouldBe(AccountNotices.None);
            second.Manager.State.Notices.ShouldBe(AccountNotices.None);
            string.Equals(await StoredRefreshTokenAsync(store).ConfigureAwait(true), seed.RefreshToken.Reveal(), StringComparison.Ordinal).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task ALostRefreshResponseIsRetriedAfterOneSecondWithoutASecondUserVisibleFailure()
    {
        var time = new ManualTimeProvider();
        var host = new FakeGoTrueHost(time) { ReuseInterval = TimeSpan.FromMinutes(1) };
        await using (host.ConfigureAwait(true))
        {
            var store = new InMemorySessionSecretStore();
            GoTrueSession seed = host.IssueSession(Email);
            using (Seat seeder = new(host, store, time, guard: null))
            {
                await seeder.Manager.SignInAsync(seed, host.Options.Issuer, Token).ConfigureAwait(true);
            }

            using Seat seat = new(host, store, time, new CrossProcessFileLock(Path.Combine(_directory, "refresh.lock")));
            List<AccountState> seen = [];
            seat.Manager.StateChanged += (_, e) =>
            {
                lock (seen)
                {
                    seen.Add(e.State);
                }
            };
            host.DropNextRefreshResponse();

            await seat.Manager.StartAsync(Token).ConfigureAwait(true);
            await UntilAsync(() => host.Count("token:refresh") >= 1).ConfigureAwait(true);
            for (int i = 0; i < 200 && host.Count("token:refresh") < 2; i++)
            {
                time.Advance(TimeSpan.FromMilliseconds(250));
                await Task.Delay(10, Token).ConfigureAwait(true);
            }

            host.Count("token:refresh").ShouldBe(2);
            await UntilAsync(() => seat.Manager.State.Connectivity == AccountConnectivity.Online).ConfigureAwait(true);
            host.PresentedRefreshTokens.ShouldBe([seed.RefreshToken.Reveal(), seed.RefreshToken.Reveal()]);
            seat.Manager.State.Phase.ShouldBe(AuthPhase.SignedIn);
            lock (seen)
            {
                seen.ShouldAllBe(state => state.Phase == AuthPhase.SignedIn && state.Connectivity != AccountConnectivity.Offline && state.Notices == AccountNotices.None);
            }
        }
    }

    [Fact]
    public void TheLockLivesInTheDesktopAdapterAndTheWebSafeLibraryHasOnlyTheNoOpGuard()
    {
        typeof(CrossProcessFileLock).Assembly.GetName().Name.ShouldBe("Autonogrammer.Accounts.Desktop");
        typeof(CrossProcessFileLock).GetInterfaces().ShouldContain(typeof(IRefreshGuard));
        typeof(IRefreshGuard).Assembly.GetName().Name.ShouldBe("Autonogrammer.Accounts");
        typeof(IRefreshGuard).Assembly.GetTypes()
            .Where(type => type is { IsInterface: false } && typeof(IRefreshGuard).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ShouldBe(["NoRefreshGuard"]);
    }

    [Fact]
    public async Task TheSecondHolderWaitsUntilTheFirstReleasesEvenAcrossInstancesOfTheLock()
    {
        string lockFile = Path.Combine(_directory, "wait.lock");
        var firstLock = new CrossProcessFileLock(lockFile);
        var secondLock = new CrossProcessFileLock(lockFile);
        IAsyncDisposable handle = await firstLock.AcquireAsync(Token).ConfigureAwait(true);

        Task<IAsyncDisposable> waiting = secondLock.AcquireAsync(Token).AsTask();
        await Task.Delay(150, Token).ConfigureAwait(true);
        waiting.IsCompleted.ShouldBeFalse();

        await handle.DisposeAsync().ConfigureAwait(true);
        await handle.DisposeAsync().ConfigureAwait(true);
        IAsyncDisposable second = await waiting.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
        await second.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AnotherProcessHoldingTheFileWithShareNoneBlocksTheLockUntilItLetsGo()
    {
        string lockFile = Path.Combine(_directory, "foreign.lock");
        _ = Directory.CreateDirectory(_directory);
        var foreign = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await using (foreign.ConfigureAwait(true))
        {
            Task<IAsyncDisposable> waiting = new CrossProcessFileLock(lockFile).AcquireAsync(Token).AsTask();
            await Task.Delay(150, Token).ConfigureAwait(true);
            waiting.IsCompleted.ShouldBeFalse();

            await foreign.DisposeAsync().ConfigureAwait(true);

            IAsyncDisposable handle = await waiting.WaitAsync(TimeSpan.FromSeconds(10), Token).ConfigureAwait(true);
            await handle.DisposeAsync().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task ACancelledWaitEndsWithoutTakingTheLock()
    {
        string lockFile = Path.Combine(_directory, "cancel.lock");
        var holder = new CrossProcessFileLock(lockFile);
        IAsyncDisposable handle = await holder.AcquireAsync(Token).ConfigureAwait(true);
        using var cancel = new CancellationTokenSource();

        Task<IAsyncDisposable> waiting = new CrossProcessFileLock(lockFile).AcquireAsync(cancel.Token).AsTask();
        await Task.Delay(100, Token).ConfigureAwait(true);
        await cancel.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(waiting).ConfigureAwait(true);
        await handle.DisposeAsync().ConfigureAwait(true);
        IAsyncDisposable again = await holder.AcquireAsync(Token).ConfigureAwait(true);
        await again.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AHolderThatNeverLetsGoEndsTheWaitWithATimeoutAfterTwoMinutes()
    {
        string lockFile = Path.Combine(_directory, "stuck.lock");
        _ = Directory.CreateDirectory(_directory);
        var time = new ManualTimeProvider();
        var foreign = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await using (foreign.ConfigureAwait(true))
        {
            CrossProcessFileLock.MaximumWait.ShouldBe(TimeSpan.FromMinutes(2));
            Task<IAsyncDisposable> waiting = new CrossProcessFileLock(lockFile, time).AcquireAsync(Token).AsTask();

            for (int i = 0; i < 400 && !waiting.IsCompleted; i++)
            {
                time.Advance(TimeSpan.FromSeconds(10));
                await Task.Delay(5, Token).ConfigureAwait(true);
            }

            await Should.ThrowAsync<TimeoutException>(waiting).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task TheLockFileIsCreatedOwnerOnlyInADirectoryTheLockCreates()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string lockFile = Path.Combine(_directory, "nested", "state", "owner.lock");
        IAsyncDisposable handle = await new CrossProcessFileLock(lockFile).AcquireAsync(Token).ConfigureAwait(true);

        File.GetUnixFileMode(lockFile).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await handle.DisposeAsync().ConfigureAwait(true);
        File.Exists(lockFile).ShouldBeTrue();
    }

    private static async Task<string> StoredRefreshTokenAsync(InMemorySessionSecretStore store)
    {
        SecretString? stored = await store.GetAsync(AccountSessionManager.SecretName, Token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(stored!.Reveal());
        return document.RootElement.GetProperty("refreshToken").GetString()!;
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 2000 && !condition(); i++)
        {
            await Task.Delay(5, Token).ConfigureAwait(false);
        }

        condition().ShouldBeTrue();
    }

    private sealed class Seat : IDisposable
    {
        private readonly HttpClient _http;

        public Seat(FakeGoTrueHost host, ISecretStore store, TimeProvider time, IRefreshGuard? guard)
        {
            _http = host.CreateClient();
            var client = new GoTrueClient(_http, host.Options, time);
            Manager = new AccountSessionManager(store, AccountSessionManager.FixedClient(client), time, guard);
        }

        public AccountSessionManager Manager { get; }

        public void Dispose()
        {
            Manager.Dispose();
            _http.Dispose();
        }
    }

    private sealed class ObservedGuard(IRefreshGuard inner) : IRefreshGuard
    {
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
        {
            _ = Requested.TrySetResult();
            return inner.AcquireAsync(cancellationToken);
        }
    }
}
