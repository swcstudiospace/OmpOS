using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Artifacts.Stores;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Presentation.Cowork;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cowork;

/// <summary>The desktop artifact composition: the account-scoped store, sign-out teardown, sync off, tool reach and panel registration.</summary>
public sealed class ArtifactCompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ag-artifact-composition-" + Guid.NewGuid().ToString("N"));
    private readonly FakeAccountGate _gate = new();
    private readonly string _accountKey = CoworkAccountFolder.KeyFromSubject("subject-a");
    private readonly string _otherKey = CoworkAccountFolder.KeyFromSubject("subject-b");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SignedOut_HasNoStoreAndOffersNoTools()
    {
        using var composition = new ArtifactsComposition(_gate, _root, null, null, null);

        composition.Store.ShouldBeNull();
        composition.Sync.ShouldBeNull();
        composition.ToolProvider.Describe().ShouldBeEmpty();
        var state = await composition.ToolProvider.GetStateAsync(Session("signed-out"), CancellationToken.None);
        state.Availability.ShouldBe(CoworkProviderAvailability.Unavailable);
        Directory.Exists(_root).ShouldBeFalse();
    }

    [Fact]
    public void SignedIn_OpensTheStoreUnderTheAccountFolder()
    {
        _gate.SignIn(_accountKey);
        using var composition = new ArtifactsComposition(_gate, _root, null, null, null);

        var store = composition.Store.ShouldNotBeNull();
        store.TenantDirectory.ShouldStartWith(CoworkAccountFolder.AccountDirectory(_root, _accountKey));
        Directory.Exists(store.TenantDirectory).ShouldBeTrue();
        composition.ToolProvider.Describe().ShouldContain(descriptor => descriptor.Name == "artifact_create");
    }

    [Fact]
    public void SignOut_TearsTheStoreDown_AndSwitchingAccountsRebuildsIt()
    {
        _gate.SignIn(_accountKey);
        using var composition = new ArtifactsComposition(_gate, _root, null, null, null);
        var first = composition.Store.ShouldNotBeNull().TenantDirectory;

        _gate.SignOut();
        composition.Reconcile();

        composition.Store.ShouldBeNull();
        composition.ToolProvider.Describe().ShouldBeEmpty();

        _gate.SignIn(_otherKey);
        composition.Reconcile();
        var second = composition.Store.ShouldNotBeNull().TenantDirectory;
        string.Equals(second, first, StringComparison.Ordinal).ShouldBeFalse();
        second.ShouldStartWith(CoworkAccountFolder.AccountDirectory(_root, _otherKey));
    }

    [Fact]
    public void Sync_IsOffByDefault_AndAbsentWithoutAnApiClient()
    {
        _gate.SignIn(_accountKey);
        using var http = new HttpClient();
        using var withClient = new ArtifactsComposition(_gate, _root, http, null, null);
        using var withoutClient = new ArtifactsComposition(_gate, Path.Combine(_root, "second"), null, null, null);

        var sync = withClient.Sync.ShouldNotBeNull();
        sync.Enabled.ShouldBeFalse();
        sync.StatusText.ShouldBe(ArtifactSyncService.SyncOffText);
        withoutClient.Sync.ShouldBeNull();
    }

    [Fact]
    public async Task ToolProvider_ReachesTheBrowserToolHost_OnlyWhenPassed()
    {
        _gate.SignIn(_accountKey);
        using var composition = new ArtifactsComposition(_gate, _root, null, null, null);
        const string sessionId = "tools-reach-host";

        using var reached = BrowserUseComposition.Wire(new BrowserUseCompositionRequest { StateDirectory = _root, Artifacts = composition.ToolProvider });
        await using (var lease = reached.Host.BeginSession(Session(sessionId)))
        {
            var offered = await reached.Host.ListOfferedAsync(sessionId, CancellationToken.None);
            offered.Any(static descriptor => string.Equals(descriptor.Name, "artifact_create", StringComparison.Ordinal)).ShouldBeTrue();
        }

        using var unchanged = BrowserUseComposition.Wire(new BrowserUseCompositionRequest { StateDirectory = _root });
        await using (var lease = unchanged.Host.BeginSession(Session(sessionId + "-absent")))
        {
            var offered = await unchanged.Host.ListOfferedAsync(sessionId + "-absent", CancellationToken.None);
            offered.Any(static descriptor => string.Equals(descriptor.Name, "artifact_create", StringComparison.Ordinal)).ShouldBeFalse();
        }
    }

    [Fact]
    public void Panel_RegistersWithTheDockShell()
    {
        using var composition = new ArtifactsComposition(_gate, _root, null, null, null);
        using var shell = new CoworkShellModel(new EmptyCatalog());

        composition.RegisterPanel(shell);

        shell.Panels.ShouldContain(panel => ReferenceEquals(panel, composition.Panel));
        shell.SelectedPanelId.ShouldBe(composition.Panel.Id);
    }

    private CoworkSessionContext Session(string sessionId) => new()
    {
        SessionId = sessionId,
        WorkingDirectory = _root,
        TrustedFolders = [],
        ModelReadsImages = false,
    };

    private sealed class FakeAccountGate : ICoworkAccountGate
    {
        private string? _key;

        public bool IsSignedIn => _key is not null;

        public string? AccountFolderKey => _key;

        public void SignIn(string key) => _key = key;

        public void SignOut() => _key = null;
    }

    private sealed class EmptyCatalog : ICoworkSessionCatalog
    {
        public event EventHandler? Changed
        {
            add
            {
            }

            remove
            {
            }
        }

        public IReadOnlyList<CoworkSessionSummary> Snapshot() => [];
    }
}
