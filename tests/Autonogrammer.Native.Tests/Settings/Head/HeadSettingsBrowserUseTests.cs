#pragma warning disable CA2007
#pragma warning disable MA0004
using System.Reflection;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.BrowserUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Head;

/// <summary>HeadSettings copies the Browser source into the window model and leaves the page list unchanged when it is absent.</summary>
public sealed class HeadSettingsBrowserUseTests
{
    /// <summary>A supplied source adds the Browser page. A null source does not.</summary>
    [Fact]
    public async Task CreateCopiesTheBrowserSourceIntoTheWindowModel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        await using var session = TestAppBuilder.StartSession();
        var without = await CaptureAsync(tree, source: null, ct).ConfigureAwait(true);
        var with = await CaptureAsync(tree, new HeadBrowserSource(), ct).ConfigureAwait(true);

        without.ShouldNotContain(SettingsSection.BrowserUse);
        with.ShouldContain(SettingsSection.BrowserUse);
        with.Count.ShouldBe(without.Count + 1);
    }

    private static async Task<List<SettingsSection>> CaptureAsync(TempTree tree, IBrowserUseSettingsSource? source, CancellationToken cancellationToken)
    {
        SettingsWindowModel? captured = null;
        await using var engines = new SessionEngineSet(new SessionEngineSetOptions
        {
            Loop = AgentLoopEnvironment.Resolve(static _ => null, tree.Workspace, tree.Workspace, tree.Root),
            AllowedKinds = [],
        });
        await using var settings = HeadSettings.Create(
            engines,
            new FileSettingsStore(tree.State),
            TimeProvider.System,
            dispatch: null,
            new HeadSettingsOptions
            {
                GetVariable = static _ => null,
                BrowserUse = source,
                WindowFactory = model =>
                {
                    captured = model;
                    return new FakeSettingsWindow(model);
                },
            });
        var host = (SettingsWindowHost)typeof(HeadSettings).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(settings)!;
        await host.ShowAsync(owner: null, cancellationToken).ConfigureAwait(true);
        return captured.ShouldNotBeNull().Pages.Select(static page => page.Section).ToList();
    }

    private sealed class HeadBrowserSource : IBrowserUseSettingsSource
    {
        public Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new BrowserUseSnapshot(BrowserUseChromiumState.Ready, "149", BrowserUseSettingsValues.Defaults, null));

        public Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AcquireAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

#pragma warning restore MA0004
#pragma warning restore CA2007
