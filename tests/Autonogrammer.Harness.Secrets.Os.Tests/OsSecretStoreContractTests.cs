using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.Secrets.Os.Tests.Support;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Os.Tests;

/// <summary>
/// Runs the <see cref="ISecretStore"/> contract against the three platform stores, each over an in-memory stand-in for its native layer (libsecret
/// fake, Keychain fake, reversible DPAPI fake over a temporary directory), and checks the typed failures, the dispatcher and the binding rules.
/// </summary>
public sealed class OsSecretStoreContractTests
{
    private const string Canary = "canary-value-7f3a91-do-not-leak";

    public static TheoryData<string> Backends => ["libsecret", "keychain", "dpapi"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void EveryFailureKindRoundTripsThroughTheExceptionWithoutAValue()
    {
        foreach (SecretStoreFailure failure in Enum.GetValues<SecretStoreFailure>())
        {
            var exception = new SecretStoreException(StoreGuards.Describe("test store", failure), failure);

            exception.Failure.ShouldBe(failure);
            exception.Message.ShouldNotContain(Canary);
            new SecretStoreException("m", failure, new InvalidOperationException("inner")).Failure.ShouldBe(failure);
        }

        new SecretStoreException().Failure.ShouldBe(SecretStoreFailure.Other);
        new SecretStoreException("m").Failure.ShouldBe(SecretStoreFailure.Other);
        new SecretStoreException("m", new InvalidOperationException("inner")).Failure.ShouldBe(SecretStoreFailure.Other);
        Enum.GetNames<SecretStoreFailure>().ShouldBe(["Unavailable", "Locked", "Denied", "Corrupt", "Other"]);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task StoredSecretIsReadBackAndListed(string backend)
    {
        using Backend store = Backend.Create(backend);

        await store.Store.SetAsync("account.session", new SecretString(Canary), Token).ConfigureAwait(true);

        (await store.Store.GetAsync("account.session", Token).ConfigureAwait(true))!.Reveal().ShouldBe(Canary);
        (await store.Store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["account.session"]);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task SettingAnExistingNameReplacesTheSecret(string backend)
    {
        using Backend store = Backend.Create(backend);
        await store.Store.SetAsync("key", new SecretString("first-value"), Token).ConfigureAwait(true);

        await store.Store.SetAsync("key", new SecretString("second-value"), Token).ConfigureAwait(true);

        (await store.Store.GetAsync("key", Token).ConfigureAwait(true))!.Reveal().ShouldBe("second-value");
        (await store.Store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["key"]);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task UnknownNameReadsAsNullAndRemovesAsFalse(string backend)
    {
        using Backend store = Backend.Create(backend);

        (await store.Store.GetAsync("missing", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.Store.RemoveAsync("missing", Token).ConfigureAwait(true)).ShouldBeFalse();
        (await store.Store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task RemovedSecretIsGoneAndASecondRemovalReportsFalse(string backend)
    {
        using Backend store = Backend.Create(backend);
        await store.Store.SetAsync("key", new SecretString("value"), Token).ConfigureAwait(true);

        (await store.Store.RemoveAsync("key", Token).ConfigureAwait(true)).ShouldBeTrue();

        (await store.Store.GetAsync("key", Token).ConfigureAwait(true)).ShouldBeNull();
        (await store.Store.RemoveAsync("key", Token).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task NamesAreCaseSensitiveAndListedInOrdinalOrder(string backend)
    {
        using Backend store = Backend.Create(backend);
        await store.Store.SetAsync("b", new SecretString("1"), Token).ConfigureAwait(true);
        await store.Store.SetAsync("B", new SecretString("2"), Token).ConfigureAwait(true);
        await store.Store.SetAsync("a", new SecretString("3"), Token).ConfigureAwait(true);
        await store.Store.SetAsync("provider.\u00e9", new SecretString("4"), Token).ConfigureAwait(true);

        (await store.Store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBe(["B", "a", "b", "provider.\u00e9"]);
        (await store.Store.GetAsync("B", Token).ConfigureAwait(true))!.Reveal().ShouldBe("2");
        (await store.Store.GetAsync("provider.\u00e9", Token).ConfigureAwait(true))!.Reveal().ShouldBe("4");
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task InvalidArgumentsFaultTheReturnedTaskInsteadOfThrowingSynchronously(string backend)
    {
        using Backend store = Backend.Create(backend);
        var secret = new SecretString("value");

        Task nullName = store.Store.SetAsync(null!, secret, Token);
        Task blankName = store.Store.GetAsync("  ", Token);
        Task nullSecret = store.Store.SetAsync("key", null!, Token);
        Task emptyRemove = store.Store.RemoveAsync(string.Empty, Token);

        await Should.ThrowAsync<ArgumentNullException>(nullName).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(blankName).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentNullException>(nullSecret).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(emptyRemove).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task ACancelledTokenCancelsTheTask(string backend)
    {
        using Backend store = Backend.Create(backend);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await AssertCancelledAsync(() => store.Store.GetAsync("key", cancelled.Token)).ConfigureAwait(true);
        await AssertCancelledAsync(() => store.Store.ListNamesAsync(cancelled.Token)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("libsecret")]
    [InlineData("keychain")]
    public async Task NativeCallsRunOffTheCallersThread(string backend)
    {
        using Backend store = Backend.Create(backend);
        Thread caller = Thread.CurrentThread;

        await store.Store.SetAsync("key", new SecretString("value"), Token).ConfigureAwait(true);
        _ = await store.Store.GetAsync("key", Token).ConfigureAwait(true);

        store.CallThreads.Count.ShouldBe(2);
        store.CallThreads.ShouldAllBe(call => !ReferenceEquals(call.Thread, caller) && !call.IsThreadPool);
    }

    [Fact]
    public async Task ALibsecretCallBlockedOnAPromptEndsWhenTheTokenIsCancelled()
    {
        var fake = new InMemoryLibsecretFake { BlockUntilCancelled = true };
        var store = new LinuxLibsecretStore(fake, null, null);
        using var cancellation = new CancellationTokenSource();

        await AssertCancelledAsync(() => store.GetAsync("key", cancellation.Token), cancellation.CancelAsync).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(SecretStoreFailure.Unavailable)]
    [InlineData(SecretStoreFailure.Locked)]
    [InlineData(SecretStoreFailure.Denied)]
    [InlineData(SecretStoreFailure.Other)]
    public async Task LibsecretFailuresSurfaceTypedWithValueFreeMessages(SecretStoreFailure expected)
    {
        var fake = new InMemoryLibsecretFake
        {
            Failure = expected switch
            {
                SecretStoreFailure.Unavailable => LinuxLibsecretStore.Failed(LibsecretErrorDomain.DBus, 2),
                SecretStoreFailure.Locked => LinuxLibsecretStore.Failed(LibsecretErrorDomain.Secret, 2),
                SecretStoreFailure.Denied => LinuxLibsecretStore.Failed(LibsecretErrorDomain.DBus, 9),
                _ => LinuxLibsecretStore.Failed(LibsecretErrorDomain.Secret, 1),
            },
        };
        var store = new LinuxLibsecretStore(fake, null, null);

        SecretStoreException set = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString(Canary), Token)).ConfigureAwait(true);
        SecretStoreException get = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("key", Token)).ConfigureAwait(true);
        SecretStoreException remove = await Should.ThrowAsync<SecretStoreException>(store.RemoveAsync("key", Token)).ConfigureAwait(true);
        SecretStoreException list = await Should.ThrowAsync<SecretStoreException>(store.ListNamesAsync(Token)).ConfigureAwait(true);

        foreach (SecretStoreException exception in new[] { set, get, remove, list })
        {
            exception.Failure.ShouldBe(expected);
            exception.Message.ShouldNotContain(Canary);
            exception.Message.ShouldNotContain("key");
            exception.ToString().ShouldNotContain(Canary);
        }
    }

    [Fact]
    public async Task AMissingLibsecretLibraryIsUnavailableNotACrash()
    {
        var store = new LinuxLibsecretStore(new InMemoryLibsecretFake { Failure = new DllNotFoundException("libsecret-1.so.0") }, null, null);

        SecretStoreException exception = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("key", Token)).ConfigureAwait(true);

        exception.Failure.ShouldBe(SecretStoreFailure.Unavailable);
    }

    [Theory]
    [InlineData(MacosKeychainStore.InteractionNotAllowed, SecretStoreFailure.Locked)]
    [InlineData(MacosKeychainStore.UserCanceled, SecretStoreFailure.Denied)]
    [InlineData(MacosKeychainStore.AuthFailed, SecretStoreFailure.Denied)]
    [InlineData(MacosKeychainStore.MissingEntitlement, SecretStoreFailure.Denied)]
    [InlineData(MacosKeychainStore.NotAvailable, SecretStoreFailure.Unavailable)]
    [InlineData(MacosKeychainStore.NoSuchKeychain, SecretStoreFailure.Unavailable)]
    [InlineData(MacosKeychainStore.NoDefaultKeychain, SecretStoreFailure.Unavailable)]
    [InlineData(MacosKeychainStore.InvalidKeychain, SecretStoreFailure.Corrupt)]
    [InlineData(MacosKeychainStore.Decode, SecretStoreFailure.Corrupt)]
    [InlineData(-1, SecretStoreFailure.Other)]
    public async Task KeychainStatusesMapToTypedFailuresWithValueFreeMessages(int status, SecretStoreFailure expected)
    {
        MacosKeychainStore.ClassifyStatus(status).ShouldBe(expected);
        var store = new MacosKeychainStore(new FakeKeychain { FixedStatus = status }, null, null);

        SecretStoreException set = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString(Canary), Token)).ConfigureAwait(true);
        SecretStoreException get = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("key", Token)).ConfigureAwait(true);

        set.Failure.ShouldBe(expected);
        get.Failure.ShouldBe(expected);
        set.Message.ShouldNotContain(Canary);
        set.ToString().ShouldNotContain(Canary);
    }

    [Fact]
    public async Task KeychainReplacesAnExistingItemByUpdate()
    {
        var keychain = new FakeKeychain();
        var store = new MacosKeychainStore(keychain, null, null);

        await store.SetAsync("key", new SecretString("first"), Token).ConfigureAwait(true);
        await store.SetAsync("key", new SecretString("second"), Token).ConfigureAwait(true);

        keychain.Adds.ShouldBe(2);
        keychain.Updates.ShouldBe(1);
        (await store.GetAsync("key", Token).ConfigureAwait(true))!.Reveal().ShouldBe("second");
    }

    [Fact]
    public async Task TheMacosStoreIsUnavailableWhereTheFrameworksAreAbsent()
    {
        Assert.SkipWhen(OperatingSystem.IsMacOS(), "The frameworks exist on macOS.");
        var store = new MacosKeychainStore();

        SecretStoreException get = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("key", Token)).ConfigureAwait(true);
        SecretStoreException set = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString(Canary), Token)).ConfigureAwait(true);

        get.Failure.ShouldBe(SecretStoreFailure.Unavailable);
        set.Failure.ShouldBe(SecretStoreFailure.Unavailable);
        set.Message.ShouldNotContain(Canary);
    }

    [Fact]
    public async Task TheWindowsStoreIsUnavailableWhereDpapiIsAbsentAndWritesNothing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "DPAPI exists on Windows.");
        string directory = Path.Combine(Path.GetTempPath(), "ag-os-secrets-" + Guid.NewGuid().ToString("N"));
        var store = new WindowsDpapiStore(directory, new ProtectedDataProtector(), null, null);

        SecretStoreException set = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString(Canary), Token)).ConfigureAwait(true);

        set.Failure.ShouldBe(SecretStoreFailure.Unavailable);
        set.Message.ShouldNotContain(Canary);
        Directory.Exists(directory).ShouldBeFalse();
    }

    [Fact]
    public async Task DpapiFilesAreNamedByHashHoldNoPlainTextAndLeaveNoTemporaryFile()
    {
        using Backend backend = Backend.Create("dpapi");

        await backend.Store.SetAsync("provider.openai", new SecretString(Canary), Token).ConfigureAwait(true);

        string[] files = Directory.GetFiles(backend.Directory!);
        files.Length.ShouldBe(1);
        Path.GetExtension(files[0]).ShouldBe(".dpapi");
        Path.GetFileName(files[0]).ShouldNotContain("openai");
        byte[] bytes = await File.ReadAllBytesAsync(files[0], Token).ConfigureAwait(true);
        Encoding.UTF8.GetString(bytes).ShouldNotContain(Canary);
        Encoding.UTF8.GetString(bytes).ShouldNotContain("openai");
    }

    [Fact]
    public async Task ADamagedDpapiFileIsCorruptNotAbsent()
    {
        using Backend backend = Backend.Create("dpapi");
        await backend.Store.SetAsync("key", new SecretString(Canary), Token).ConfigureAwait(true);
        string file = Directory.GetFiles(backend.Directory!).Single();
        await File.WriteAllBytesAsync(file, [1, 2, 3, 4, 5], Token).ConfigureAwait(true);

        SecretStoreException get = await Should.ThrowAsync<SecretStoreException>(backend.Store.GetAsync("key", Token)).ConfigureAwait(true);
        SecretStoreException list = await Should.ThrowAsync<SecretStoreException>(backend.Store.ListNamesAsync(Token)).ConfigureAwait(true);

        get.Failure.ShouldBe(SecretStoreFailure.Corrupt);
        list.Failure.ShouldBe(SecretStoreFailure.Corrupt);
        get.Message.ShouldNotContain(Canary);
    }

    [Fact]
    public async Task ADpapiFileMovedToAnotherNamesPathIsCorrupt()
    {
        using Backend backend = Backend.Create("dpapi");
        await backend.Store.SetAsync("alpha", new SecretString(Canary), Token).ConfigureAwait(true);
        string source = Directory.GetFiles(backend.Directory!).Single();
        string target = Path.Combine(backend.Directory!, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("beta"))) + ".dpapi");
        File.Move(source, target);

        SecretStoreException get = await Should.ThrowAsync<SecretStoreException>(backend.Store.GetAsync("beta", Token)).ConfigureAwait(true);

        get.Failure.ShouldBe(SecretStoreFailure.Corrupt);
    }

    [Fact]
    public async Task ADirectoryThatCannotBeUsedIsReportedAsAFailureNotACrash()
    {
        string file = Path.Combine(Path.GetTempPath(), "ag-os-secrets-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(file, "not a directory", Token).ConfigureAwait(true);
        try
        {
            var store = new WindowsDpapiStore(Path.Combine(file, "secrets"), new XorProtector(), null, null);

            SecretStoreException set = await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString(Canary), Token)).ConfigureAwait(true);

            set.Failure.ShouldBe(SecretStoreFailure.Other);
            set.Message.ShouldNotContain(Canary);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("Linux", typeof(LinuxLibsecretStore))]
    [InlineData("MacOs", typeof(MacosKeychainStore))]
    [InlineData("Windows", typeof(WindowsDpapiStore))]
    [InlineData("Unsupported", typeof(UnavailableSecretStore))]
    public void TheDispatcherSelectsTheStoreOfThePlatform(string platform, Type expected)
    {
        var store = new OsSecretStore(Enum.Parse<OsSecretStore.Platform>(platform), null, null);

        store.Backend.ShouldBeOfType(expected);
    }

    [Fact]
    public void TheDispatcherDetectsTheCurrentPlatform()
    {
        OsSecretStore.Platform expected = OperatingSystem.IsLinux() ? OsSecretStore.Platform.Linux
            : OperatingSystem.IsMacOS() ? OsSecretStore.Platform.MacOs
            : OperatingSystem.IsWindows() ? OsSecretStore.Platform.Windows
            : OsSecretStore.Platform.Unsupported;

        OsSecretStore.Detect().ShouldBe(expected);
        new OsSecretStore().Backend.ShouldNotBeNull();
    }

    [Fact]
    public async Task ThePublicDispatcherDelegatesToTheBackendOfThePlatform()
    {
        var store = new OsSecretStore(OsSecretStore.Platform.Unsupported, null, null);

        SecretStoreException exception = await Should.ThrowAsync<SecretStoreException>(store.GetAsync("key", Token)).ConfigureAwait(true);

        exception.Failure.ShouldBe(SecretStoreFailure.Unavailable);
    }

    [Fact]
    public async Task AStoreOfAnUnsupportedPlatformFailsAsUnavailableAfterCheckingArguments()
    {
        UnavailableSecretStore store = new();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        (await Should.ThrowAsync<SecretStoreException>(store.SetAsync("key", new SecretString("v"), Token)).ConfigureAwait(true)).Failure.ShouldBe(SecretStoreFailure.Unavailable);
        (await Should.ThrowAsync<SecretStoreException>(store.RemoveAsync("key", Token)).ConfigureAwait(true)).Failure.ShouldBe(SecretStoreFailure.Unavailable);
        (await Should.ThrowAsync<SecretStoreException>(store.ListNamesAsync(Token)).ConfigureAwait(true)).Failure.ShouldBe(SecretStoreFailure.Unavailable);
        await Should.ThrowAsync<ArgumentException>(store.GetAsync(" ", Token)).ConfigureAwait(true);
        await AssertCancelledAsync(() => store.GetAsync("key", cancelled.Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task LogMessagesCarryNeitherTheNameNorTheValueAndReportTheMeasuredDuration()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var fake = new InMemoryLibsecretFake { OnCall = () => time.Advance(TimeSpan.FromMilliseconds(250)) };
        var logger = new CapturingLogger();
        var store = new LinuxLibsecretStore(fake, logger, time);

        await store.SetAsync("account.session", new SecretString(Canary), Token).ConfigureAwait(true);
        fake.Failure = LinuxLibsecretStore.Failed(LibsecretErrorDomain.Secret, 2);
        await Should.ThrowAsync<SecretStoreException>(store.GetAsync("account.session", Token)).ConfigureAwait(true);
        _ = OsSecretStore.CreateBackend(OsSecretStore.Platform.Unsupported, logger, time);

        logger.Entries.Count.ShouldBe(3);
        logger.Entries[0].Message.ShouldBe("libsecret Set completed in 250 ms.");
        logger.Entries[1].Message.ShouldBe("libsecret Get failed: Locked.");
        logger.Entries[2].Message.ShouldBe("No operating system secret store exists for this platform.");
        logger.Entries.ShouldAllBe(entry => entry.EventId.Id >= 3050 && entry.EventId.Id <= 3069);
        logger.Entries.ShouldAllBe(entry => !entry.Message.Contains(Canary, StringComparison.Ordinal) && !entry.Message.Contains("account.session", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryNativeImportRestrictsLibraryProbingToSafeDirectories()
    {
        const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        MethodInfo[] imports =
        [
            .. typeof(OsSecretStore).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(All))
                .Where(method => method.Attributes.HasFlag(MethodAttributes.PinvokeImpl)),
        ];

        imports.Length.ShouldBeGreaterThan(30);
        foreach (MethodInfo import in imports)
        {
            string site = $"{import.DeclaringType!.Name}.{import.Name}";
            DefaultDllImportSearchPathsAttribute? attribute = import.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>();
            attribute.ShouldNotBeNull($"{site} must carry DefaultDllImportSearchPaths");
            attribute.Paths.ShouldBe(DllImportSearchPath.SafeDirectories, site);
        }
    }

    [Fact]
    public async Task NoProjectOfThePlanAllowsUnsafeCodeAndNoSourceUsesIt()
    {
        string root = RepositoryRoot();
        string needle = "Allow" + "UnsafeBlocks";
        string[] files =
        [
            Path.Combine(root, "Directory.Build.props"),
            Path.Combine(root, "src", "Autonogrammer.Harness.Secrets", "Autonogrammer.Harness.Secrets.csproj"),
            Path.Combine(root, "src", "Autonogrammer.Harness.Secrets.Os", "Autonogrammer.Harness.Secrets.Os.csproj"),
            Path.Combine(root, "tests", "Autonogrammer.Harness.Secrets.Os.Tests", "Autonogrammer.Harness.Secrets.Os.Tests.csproj"),
        ];
        foreach (string file in files)
        {
            (await File.ReadAllTextAsync(file, Token).ConfigureAwait(true)).ShouldNotContain(needle, Case.Sensitive, file);
        }

        string unsafeKeyword = "un" + "safe ";
        string functionPointer = "dele" + "gate*";
        foreach (string source in Directory.EnumerateFiles(Path.Combine(root, "src", "Autonogrammer.Harness.Secrets.Os"), "*.cs", SearchOption.TopDirectoryOnly))
        {
            string text = await File.ReadAllTextAsync(source, Token).ConfigureAwait(true);
            text.ShouldNotContain(unsafeKeyword, Case.Sensitive, source);
            text.ShouldNotContain(functionPointer, Case.Sensitive, source);
        }
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Autonogrammer.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root was not found above the test output directory.");
    }

    private static async Task AssertCancelledAsync(Func<Task> start, Func<Task>? whileRunning = null)
    {
        Task task = start();
        if (whileRunning is not null)
        {
            task.IsCompleted.ShouldBeFalse();
            await whileRunning().ConfigureAwait(true);
        }

        _ = await Task.WhenAny(task).ConfigureAwait(true);

        task.IsCanceled.ShouldBeTrue();
    }

    private sealed class Backend : IDisposable
    {
        private readonly Func<IReadOnlyList<(Thread Thread, bool IsThreadPool)>> _callThreads;

        private Backend(ISecretStore store, string? directory, Func<IReadOnlyList<(Thread Thread, bool IsThreadPool)>> callThreads)
        {
            Store = store;
            Directory = directory;
            _callThreads = callThreads;
        }

        public ISecretStore Store { get; }

        public string? Directory { get; }

        public IReadOnlyList<(Thread Thread, bool IsThreadPool)> CallThreads => _callThreads();

        public static Backend Create(string kind)
        {
            switch (kind)
            {
                case "libsecret":
                    var fake = new InMemoryLibsecretFake();
                    return new Backend(new LinuxLibsecretStore(fake, null, null), null, () => fake.CallThreads);
                case "keychain":
                    var keychain = new FakeKeychain();
                    return new Backend(new MacosKeychainStore(keychain, null, null), null, () => keychain.CallThreads);
                case "dpapi":
                    string directory = Path.Combine(Path.GetTempPath(), "ag-os-secrets-" + Guid.NewGuid().ToString("N"));
                    return new Backend(new WindowsDpapiStore(directory, new XorProtector(), null, null), directory, () => []);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public void Dispose()
        {
            if (Directory is not null && System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private sealed class FakeKeychain : IKeychainApi
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);
        private readonly List<(Thread Thread, bool IsThreadPool)> _threads = [];

        public int? FixedStatus { get; set; }

        public int Adds { get; private set; }

        public int Updates { get; private set; }

        public IReadOnlyList<(Thread Thread, bool IsThreadPool)> CallThreads
        {
            get
            {
                lock (_gate)
                {
                    return [.. _threads];
                }
            }
        }

        public int Add(string account, SecretString secret)
        {
            lock (_gate)
            {
                _threads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
                Adds++;
                if (FixedStatus is { } fixedStatus)
                {
                    return fixedStatus;
                }

                if (_items.ContainsKey(account))
                {
                    return MacosKeychainStore.DuplicateItem;
                }

                _items[account] = secret.Reveal();
                return MacosKeychainStore.Success;
            }
        }

        public int Update(string account, SecretString secret)
        {
            lock (_gate)
            {
                _threads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
                Updates++;
                if (!_items.ContainsKey(account))
                {
                    return MacosKeychainStore.ItemNotFound;
                }

                _items[account] = secret.Reveal();
                return MacosKeychainStore.Success;
            }
        }

        public int Copy(string account, out SecretString? secret)
        {
            lock (_gate)
            {
                _threads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
                secret = null;
                if (FixedStatus is { } fixedStatus)
                {
                    return fixedStatus;
                }

                if (!_items.TryGetValue(account, out string? value))
                {
                    return MacosKeychainStore.ItemNotFound;
                }

                secret = new SecretString(value);
                return MacosKeychainStore.Success;
            }
        }

        public int Delete(string account)
        {
            lock (_gate)
            {
                _threads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
                return _items.Remove(account) ? MacosKeychainStore.Success : MacosKeychainStore.ItemNotFound;
            }
        }

        public int List(out IReadOnlyList<string> accounts)
        {
            lock (_gate)
            {
                _threads.Add((Thread.CurrentThread, Thread.CurrentThread.IsThreadPoolThread));
                accounts = [.. _items.Keys];
                return _items.Count == 0 ? MacosKeychainStore.ItemNotFound : MacosKeychainStore.Success;
            }
        }
    }

    /// <summary>A reversible stand-in for DPAPI: a marker byte followed by the bytes xor-ed with a constant. Unprotect refuses bytes without the marker.</summary>
    private sealed class XorProtector : IDpapiProtector
    {
        private const byte Marker = 0xA5;
        private const byte Mask = 0x5A;

        public byte[] Protect(byte[] plain) => [Marker, .. plain.Select(static b => (byte)(b ^ Mask))];

        public byte[] Unprotect(byte[] protectedBytes) =>
            protectedBytes is [Marker, ..]
                ? [.. protectedBytes.Skip(1).Select(static b => (byte)(b ^ Mask))]
                : throw new CryptographicException("The data is not protected by this user.");
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, eventId, formatter(state, exception)));
    }
}
