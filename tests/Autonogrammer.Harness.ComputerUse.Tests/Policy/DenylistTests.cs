using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

using Shouldly;

using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Built-in denylist, user additions and list-window titles.</summary>
public sealed class DenylistTests
{
    [Fact]
    public async Task BuiltInRowsCoverTheRequiredGroupsAndCannotBeRemoved()
    {
        AppCatalogue catalogue = await LoadAsync().ConfigureAwait(true);
        string[] required =
        [
            "xterm",
            "gnome-terminal",
            "1password",
            "gnome-keyring",
            "gnome-control-center",
            "polkit-gnome-authentication-agent-1",
            "tradingview",
            "electrum",
            "autonogrammer-own-process",
        ];
        foreach (string identity in required)
        {
            catalogue.BuiltInRows.ShouldContain(row => row.AppIdentity == identity && row.BuiltIn);
            catalogue.IsDenied(identity).ShouldBeTrue();
        }

        await Should.ThrowAsync<InvalidOperationException>(() => catalogue.RemoveAsync("xterm", TestContext.Current.CancellationToken).AsTask()).ConfigureAwait(true);
        catalogue.IsDenied("xterm").ShouldBeTrue();
    }

    [Fact]
    public async Task WindowsAndMacOsIdentifiersAreUnverified()
    {
        AppCatalogue catalogue = await LoadAsync().ConfigureAwait(true);
        catalogue.Document.Categories.SelectMany(static category => category.Identifiers)
            .Where(static row => row.Platform is "windows" or "macos")
            .ShouldAllBe(static row => row.Unverified);
        catalogue.Document.Denylist.Where(static row => row.Platform is "windows" or "macos")
            .ShouldAllBe(static row => row.Unverified);
    }

    [Fact]
    public async Task UserAdditionsExtendAndDenyWinsOverAGrant()
    {
        MemorySettings settings = new();
        AppCatalogue catalogue = await AppCatalogue.LoadAsync(settings, TestContext.Current.CancellationToken).ConfigureAwait(true);
        AppCatalogue store = catalogue;
        await store.AddAsync("custom-app", TestContext.Current.CancellationToken).ConfigureAwait(true);
        store.BuiltInRows.ShouldNotContain(row => row.AppIdentity == "custom-app");
        catalogue.IsDenied("custom-app").ShouldBeTrue();

        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GrantStore grants = new(time);
        grants.TryCreateFromUser("session", "xterm", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, false, out _);
        catalogue.IsDenied("xterm").ShouldBeTrue();
        grants.Snapshot().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListWindowsShowsTitlesOnlyForGrantedApplications()
    {
        AppCatalogue catalogue = await LoadAsync().ConfigureAwait(true);
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GrantStore grants = new(time);
        grants.TryCreateFromUser("session", "code", ComputerGrantTier.ClickOnly, ComputerActionOrigin.Local, null, false, out _);
        WindowInfo[] windows =
        [
            Window("granted", "code", "Editor", false),
            Window("hidden", "firefox", "Secret tab", false),
            Window("own", "autonogrammer", "Phrase", true),
        ];
        WindowListView view = catalogue.ProjectWindows(windows, grants.Snapshot());
        view.Titles.ShouldBe(["Editor"], ignoreOrder: false);
        view.Titles.ShouldNotContain("Secret tab", StringComparer.Ordinal);
        view.Titles.ShouldNotContain("Phrase", StringComparer.Ordinal);
        view.UngrantedCategoryCounts["browser"].ShouldBe(1);
        view.UngrantedCategoryCounts["own-process"].ShouldBe(1);
    }

    [Fact]
    public async Task DenylistReadsWorkThroughThePort()
    {
        AppCatalogue catalogue = await LoadAsync().ConfigureAwait(true);
        AppCatalogue store = catalogue;
        store.BuiltInRows.ShouldContain(row => row.BuiltIn && row.AppIdentity == "bitwarden");
        await store.AddAsync("extra", TestContext.Current.CancellationToken).ConfigureAwait(true);
        catalogue.IsDenied("extra").ShouldBeTrue();
    }

    private static async Task<AppCatalogue> LoadAsync() =>
        await AppCatalogue.LoadAsync(new MemorySettings(), TestContext.Current.CancellationToken).ConfigureAwait(true);

    private static WindowInfo Window(string id, string identity, string title, bool own) => new()
    {
        Id = id,
        Pid = 1,
        AppIdentity = identity,
        Title = title,
        Bounds = new PixelRect { X = 0, Y = 0, Width = 10, Height = 10 },
        Minimized = false,
        Focused = false,
        IsOwnProcess = own,
    };

    private sealed class MemorySettings : IComputerUseSettingsStore
    {
        private ComputerUseUserSettings _settings = new();

        public ValueTask<ComputerUseUserSettings> LoadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(_settings);

        public ValueTask SaveAsync(ComputerUseUserSettings settings, CancellationToken cancellationToken)
        {
            _settings = settings;
            return ValueTask.CompletedTask;
        }
    }
}
