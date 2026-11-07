#pragma warning disable CA2007
#pragma warning disable MA0004
using Autonogrammer.Cowork;
using Autonogrammer.Harness.ComputerUse;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Composition.ComputerUse;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>Platform selection and the settings store and factory lifetimes owned by the head.</summary>
public sealed class ComputerUseHeadCompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ag-73-06-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ComputerPlatform.X11, typeof(Autonogrammer.Harness.ComputerUse.X11.X11Backend))]
    [InlineData(ComputerPlatform.Windows, typeof(Autonogrammer.Harness.ComputerUse.Windows.WindowsBackend))]
    [InlineData(ComputerPlatform.MacOS, typeof(Autonogrammer.Harness.ComputerUse.MacOS.MacOSBackend))]
    public void TheBackendMatchesTheInjectedPlatform(ComputerPlatform platform, Type backend)
    {
        ComputerUseHeadComposition.ChooseBackend(platform).ShouldBeOfType(backend);
    }

    [Fact]
    public async Task AnUnsupportedPlatformUsesTheNullBackend()
    {
        using var result = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });

        result.Backend.ShouldBeOfType<NullComputerUseBackend>();
        var availability = await result.Status.GetAvailabilityAsync(CancellationToken.None);
        availability.ShouldBe(ComputerAvailability.UnsupportedPlatform);
        ComputerAvailabilityRemediation.For(availability).ShouldBe("computer.unavailable.platform");
    }

    [Fact]
    public void TheAccessFactsAdapterReadsOnlyTheAccountGate()
    {
        var gate = new FakeGate(signedIn: true);
        var adapter = new ComputerAccessFactsAdapter(gate);

        adapter.IsSignedIn.ShouldBeTrue();
        gate.SignedInReads.ShouldBe(1);
        adapter.IsCoworkSession("cowork-1").ShouldBeTrue();
        gate.SignedInReads.ShouldBe(2);
        gate.FolderReads.ShouldBe(0);
    }

    [Fact]
    public async Task TheDefaultStoreLivesUntilItsResultIsDisposed()
    {
        using var result = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var store = result.Settings.ShouldBeOfType<FileComputerUseSettingsStore>();
        await store.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "owned" }, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("owned");

        result.Dispose();
        result.Dispose();

        var resultFailure = Should.Throw<ObjectDisposedException>(() => { _ = result.Settings; });
        resultFailure.ObjectName.ShouldBe(typeof(ComputerUseHeadResult).FullName);
        var loadFailure = await Should.ThrowAsync<ObjectDisposedException>(async () =>
        {
            await store.LoadAsync(CancellationToken.None);
        });
        var saveFailure = await Should.ThrowAsync<ObjectDisposedException>(async () =>
        {
            await store.SaveAsync(new ComputerUseUserSettings(), CancellationToken.None);
        });
        loadFailure.ObjectName.ShouldBe(typeof(FileComputerUseSettingsStore).FullName);
        saveFailure.ObjectName.ShouldBe(typeof(FileComputerUseSettingsStore).FullName);

        using var reloaded = new FileComputerUseSettingsStore(_root);
        (await reloaded.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("owned");
    }

    [Fact]
    public async Task ASuppliedConcreteStoreRemainsBorrowedAfterResultDisposal()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        using var result = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = string.Empty,
            AccountGate = new FakeGate(signedIn: false),
            Settings = store,
        });
        var borrowed = result.Settings;
        borrowed.ShouldBeSameAs(store);
        await borrowed.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "borrowed" }, CancellationToken.None);

        result.Dispose();
        result.Dispose();

        Should.Throw<ObjectDisposedException>(() => { _ = result.Settings; });
        (await borrowed.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("borrowed");
        await borrowed.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "still-owned-by-caller" }, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("still-owned-by-caller");
    }

    [Fact]
    public async Task ASuppliedDisposableInterfaceIsNotDisposedByTheResult()
    {
        using var store = new DisposableSettingsStore(_root);
        using var result = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = string.Empty,
            AccountGate = new FakeGate(signedIn: false),
            Settings = store,
        });
        result.Settings.ShouldBeSameAs(store);
        await result.Settings.SaveAsync(new ComputerUseUserSettings { KillChord = "Ctrl+Alt+Esc" }, CancellationToken.None);

        result.Dispose();
        result.Dispose();

        store.DisposeCalls.ShouldBe(0);
        (await store.LoadAsync(CancellationToken.None)).KillChord.ShouldBe("Ctrl+Alt+Esc");
        await store.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "caller" }, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("caller");
    }

    [Fact]
    public void AChromeSubscriberFailurePrecedesDefaultStoreCreationAndPreservesTheLiveFactory()
    {
        using var live = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var factory = ComputerUseHeadComposition.SettingsFactory;
        factory.ShouldNotBeNull();
        var expected = new InvalidOperationException("Chrome subscriber rejected registration");
        var chrome = new SurfaceChromeModel();
        chrome.Changed += (_, _) => throw expected;

        var failure = Should.Throw<InvalidOperationException>(() =>
        {
            using var failed = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
            {
                Platform = ComputerPlatform.Unsupported,
                // Constructing a default store with this path would instead throw ArgumentException.
                StateDirectory = string.Empty,
                AccountGate = new FakeGate(signedIn: false),
                Chrome = chrome,
            });
        });

        failure.ShouldBeSameAs(expected);
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(factory);
        factory!().ShouldBeNull();
    }

    [Fact]
    public async Task AFailedBuildLeavesASuppliedDisposableStoreAndTheLiveFactoryAlone()
    {
        using var live = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var factory = ComputerUseHeadComposition.SettingsFactory;
        using var store = new DisposableSettingsStore(_root);
        await store.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "before-failure" }, CancellationToken.None);
        var expected = new InvalidOperationException("Chrome subscriber rejected registration");
        var chrome = new SurfaceChromeModel();
        chrome.Changed += (_, _) => throw expected;

        Should.Throw<InvalidOperationException>(() =>
        {
            using var failed = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
            {
                Platform = ComputerPlatform.Unsupported,
                StateDirectory = _root,
                AccountGate = new FakeGate(signedIn: false),
                Settings = store,
                Chrome = chrome,
            });
        }).ShouldBeSameAs(expected);

        store.DisposeCalls.ShouldBe(0);
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(factory);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("before-failure");
        await store.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "after-failure" }, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("after-failure");
    }

    [Fact]
    public void InvalidDefaultStoreConstructionDoesNotReplaceTheLiveFactory()
    {
        using var live = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var factory = ComputerUseHeadComposition.SettingsFactory;

        Should.Throw<ArgumentException>(() =>
        {
            using var failed = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
            {
                Platform = ComputerPlatform.Unsupported,
                StateDirectory = string.Empty,
                AccountGate = new FakeGate(signedIn: false),
            });
        });

        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(factory);
        factory!().ShouldBeNull();
    }

    [Fact]
    public void DisposingTheCurrentOwnerRestoresItsLivePredecessor()
    {
        var initial = ComputerUseHeadComposition.SettingsFactory;
        using var older = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var olderFactory = ComputerUseHeadComposition.SettingsFactory;
        using var newer = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var newerFactory = ComputerUseHeadComposition.SettingsFactory;
        olderFactory.ShouldNotBeNull();
        newerFactory.ShouldNotBeNull();
        ReferenceEquals(olderFactory, newerFactory).ShouldBeFalse();
        newerFactory!().ShouldBeNull();

        newer.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(olderFactory);
        olderFactory!().ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => newerFactory!());
        newer.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(olderFactory);

        older.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(initial);
        Should.Throw<ObjectDisposedException>(() => olderFactory!());
    }

    [Fact]
    public async Task DisposingAnOlderOwnerDoesNotClobberANewerBorrowedStoreOwner()
    {
        var initial = ComputerUseHeadComposition.SettingsFactory;
        using var store = new FileComputerUseSettingsStore(_root);
        using var older = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var olderFactory = ComputerUseHeadComposition.SettingsFactory;
        using var newer = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
            Settings = store,
        });
        var newerFactory = ComputerUseHeadComposition.SettingsFactory;

        older.Dispose();

        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(newerFactory);
        newerFactory!().ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => olderFactory!());
        newer.Settings.ShouldBeSameAs(store);
        await newer.Settings.SaveAsync(new ComputerUseUserSettings { SecurityPhrase = "newer-borrowed" }, CancellationToken.None);
        (await newer.Settings.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("newer-borrowed");

        newer.Dispose();

        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(initial);
        Should.Throw<ObjectDisposedException>(() => newerFactory!());
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("newer-borrowed");
        older.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(initial);
    }

    [Fact]
    public void RestorationSkipsAllDisposedPredecessors()
    {
        using var live = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var liveFactory = ComputerUseHeadComposition.SettingsFactory;
        using var first = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        using var second = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        using var current = ComputerUseHeadComposition.Build(new ComputerUseHeadOptions
        {
            Platform = ComputerPlatform.Unsupported,
            StateDirectory = _root,
            AccountGate = new FakeGate(signedIn: false),
        });
        var currentFactory = ComputerUseHeadComposition.SettingsFactory;

        first.Dispose();
        second.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(currentFactory);

        current.Dispose();

        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(liveFactory);
        liveFactory!().ShouldBeNull();
        Should.Throw<ObjectDisposedException>(() => currentFactory!());
        first.Dispose();
        second.Dispose();
        current.Dispose();
        ComputerUseHeadComposition.SettingsFactory.ShouldBeSameAs(liveFactory);
    }

    private sealed class DisposableSettingsStore : IComputerUseSettingsStore, IDisposable
    {
        private readonly FileComputerUseSettingsStore _store;

        public DisposableSettingsStore(string root) => _store = new FileComputerUseSettingsStore(root);

        public int DisposeCalls { get; private set; }

        public ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken) =>
            _store.LoadAsync(cancellationToken);

        public ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken) =>
            _store.SaveAsync(settings, cancellationToken);

        public void Dispose()
        {
            DisposeCalls++;
            _store.Dispose();
        }
    }

    private sealed class FakeGate : ICoworkAccountGate
    {
        private readonly bool _signedIn;

        public FakeGate(bool signedIn) => _signedIn = signedIn;

        public int SignedInReads { get; private set; }

        public int FolderReads { get; private set; }

        public bool IsSignedIn
        {
            get
            {
                SignedInReads++;
                return _signedIn;
            }
        }

        public string? AccountFolderKey
        {
            get
            {
                FolderReads++;
                return null;
            }
        }
    }
}

#pragma warning restore CA2007
#pragma warning restore MA0004
