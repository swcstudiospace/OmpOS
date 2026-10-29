using Autonogrammer.Native.Tests.AgentPanel.Shell;
using Autonogrammer.Native.Tests.Checkpoints;
using Autonogrammer.Native.Tests.Cloud;
using Autonogrammer.Native.Tests.Display;
using Autonogrammer.Native.Tests.Parity;
using Autonogrammer.Native.Tests.SlashPalette;
using Autonogrammer.Native.Tests.Worktrees;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.Remote;
using Autonogrammer.Workspace.Sets;
using Avalonia.Controls;
using Shouldly;
using Xunit;
using IAboutLauncher = Autonogrammer.Native.Views.IAboutLauncher;
using IUrlLauncher = Autonogrammer.Native.Views.IUrlLauncher;

namespace Autonogrammer.Native.Tests.Shell;

/// <summary>The native slash commands the shell registers handlers for: each listed command runs something observable, and a command the window cannot run is listed disabled.</summary>
public sealed class ShellSlashHandlerTests
{
    /// <summary>The commands that need an engine feature the scripted sessions of these tests lack: a session fork and a side chat engine.</summary>
    private static readonly string[] NeedEngineFeatures = ["fork", "branch", "duplicate", "side", "btw"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>In a window with every service, each command that is not Unavailable and runs a native action is enabled in the palette, except the ones that need the engine to fork or answer side chats.</summary>
    [Fact]
    public async Task EveryListedNativeCommandIsEnabledOrNeedsTheEngine()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);
            var composer = rig.Fixture.Runtime("session-1").Composer;

            composer.Draft = "/";
            var palette = composer.SlashPalette.ShouldNotBeNull();
            var native = SlashCommandCatalogue.Shipped.ListAll()
                .Where(static command => command.Kind is SlashHandlerKind.NativeAction or SlashHandlerKind.CloudHandoff && command.Status != SlashCommandStatus.Unavailable)
                .ToArray();

            native.Length.ShouldBeGreaterThan(30);
            foreach (var command in native)
            {
                var entry = palette.Entries.Single(candidate => string.Equals(candidate.Name, command.Name, StringComparison.Ordinal));
                var needsEngine = NeedEngineFeatures.Contains(command.Name, StringComparer.Ordinal);
                entry.IsEnabled.ShouldBe(!needsEngine, $"/{command.Name} ({command.HandlerId})");
                if (needsEngine)
                {
                    entry.DisabledReason.ShouldBe(SlashPaletteCopy.ActionNotInThisWindow);
                }
            }
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/about opens the About launcher over the window.</summary>
    [Fact]
    public async Task AboutOpensTheAboutLauncher()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);

            await rig.SendAsync("/about").ConfigureAwait(true);

            rig.About.Owners.Count.ShouldBe(1);
            rig.About.Owners[0].ShouldBeSameAs(rig.Fixture.ServicesWindow);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/feedback opens the issue form in the browser with the typed title.</summary>
    [Fact]
    public async Task FeedbackOpensTheIssueFormWithTheTitle()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);

            await rig.SendAsync("/feedback the rail flickers").ConfigureAwait(true);

            var opened = rig.Urls.Opened.ShouldHaveSingleItem();
            opened.AbsoluteUri.ShouldBe(FeedbackSlashHandler.DefaultIssuesUrl + "?title=the%20rail%20flickers");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The display commands flip the saved switches; the rewind switch gates /rewind.</summary>
    [Fact]
    public async Task DisplayCommandsFlipTheSwitchesAndRewindFollowsItsSwitch()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);
            rig.Display.ShowLineNumbers.ShouldBeTrue();

            await rig.SendAsync("/line-numbers off").ConfigureAwait(true);
            rig.Display.ShowLineNumbers.ShouldBeFalse();
            rig.Store.Saved[^1].ShowLineNumbers.ShouldBeFalse();

            await rig.SendAsync("/status-indicators off").ConfigureAwait(true);
            rig.Display.ShowStatusIndicators.ShouldBeFalse();

            await rig.Display.SetAsync(DisplaySetting.Rewind, false, Token).ConfigureAwait(true);
            await rig.SendAsync("/rewind").ConfigureAwait(true);
            rig.StatusText.ShouldBe(HandlerCopy.RewindOff);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/rewind opens the Checkpoints timeline of the session over its backend.</summary>
    [Fact]
    public async Task RewindOpensTheCheckpointsTimeline()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);
            var toggle = rig.Main.FindControl<Avalonia.Controls.Primitives.ToggleButton>("CheckpointsToggle")!;
            toggle.IsChecked.ShouldNotBe(true);

            await rig.SendAsync("/rewind").ConfigureAwait(true);

            toggle.IsChecked.ShouldBe(true, "the timeline is open and the Tools toggle says so");
            rig.StatusText.ShouldBe(HandlerCopy.CheckpointsOpened);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/save-workspace saves the session's folder under the name; /load-workspace hands the set's folders to the next session and starts it.</summary>
    [Fact]
    public async Task WorkspaceSetsSaveAndLoad()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);

            await rig.SendAsync("/save-workspace demo").ConfigureAwait(true);
            var saved = await rig.Sets.ListAsync(Token).ConfigureAwait(true);
            saved.Select(static recent => recent.Set.Name).ShouldBe(["demo"]);

            var sessionsBefore = rig.Fixture.Host.Runtimes.Count;
            await rig.SendAsync("/load-workspace demo").ConfigureAwait(true);

            rig.Fixture.Host.Runtimes.Count.ShouldBeGreaterThan(sessionsBefore, "a new session started");
            await rig.SendAsync("/load-workspace nothing-saved-here").ConfigureAwait(true);
            rig.StatusText.ShouldContain("nothing-saved-here", Case.Sensitive);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>/in-cloud asks for the confirmation dialog with the typed task, and answers with what the dialog decided.</summary>
    [Fact]
    public async Task InCloudShowsTheDialogAndAnswersItsResult()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);
            CloudHandoffModel? shown = null;
            rig.Main.CloudHandoffDialog = (model, _) =>
            {
                shown = model;
                return Task.FromResult<CloudHandoffResult?>(new CloudHandoffResult(CloudHandoffOutcome.Started, "Started cloud agent demo with your task.", CloudAgents.Agent("9", CloudAgentStatus.Starting, "demo")));
            };

            await rig.SendAsync("/in-cloud fix the login bug").ConfigureAwait(true);

            shown.ShouldNotBeNull().Task.ShouldBe("fix the login bug");
            shown.CostNotice.ShouldBe(CloudCopy.CostNotice(rig.Cloud.Setup.DefaultIdleMinutes));
            shown.CanStart.ShouldBeFalse("nothing is started before the repository and the cost are confirmed");
            rig.Cloud.CreateRequests.ShouldBeEmpty();
            rig.StatusText.ShouldBe("Started cloud agent demo with your task.");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The &amp; prefix runs the same hand-off; a dialog that was cancelled says so and a head with no cloud configured says why.</summary>
    [Fact]
    public async Task TheAmpersandPrefixHandsOffAndSaysWhyWhenItCannot()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var rig = await Rig.CreateAsync().ConfigureAwait(true);
            await using var rigScope = rig.ConfigureAwait(true);
            rig.Main.CloudHandoffDialog = (_, _) => Task.FromResult<CloudHandoffResult?>(null);

            await rig.SendAsync("& refactor the auth module").ConfigureAwait(true);
            rig.StatusText.ShouldBe(CloudCopy.CreateCancelled);

            rig.Cloud.Setup = CloudSetup.NotConfigured("The Autonogrammer API address is not set.");
            await rig.SendAsync("& refactor the auth module").ConfigureAwait(true);
            rig.StatusText.ShouldContain("The Autonogrammer API address is not set.", Case.Sensitive);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no cloud host at all the hand-off says that cloud agents are not set up, and opens no dialog.</summary>
    [Fact]
    public async Task WithNoCloudHostTheHandoffSaysWhy()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(), SessionCommands = new FakeSessionCommands() });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var dialogs = 0;
            main.CloudHandoffDialog = (_, _) =>
            {
                dialogs++;
                return Task.FromResult<CloudHandoffResult?>(null);
            };
            var runtime = fixture.Runtime("session-1");

            runtime.Composer.Draft = "/in-cloud fix it";
            await runtime.Composer.SendAsync().ConfigureAwait(true);

            runtime.StatusText.ShouldContain(CloudCopy.NotConfiguredShort, Case.Sensitive);
            dialogs.ShouldBe(0);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Everything a full window needs, over fakes.</summary>
    private sealed class Rig : IAsyncDisposable
    {
        private readonly string _directory;

        private Rig(ShellFixture fixture, MainView main, string directory, RecordingAbout about, RecordingUrls urls, FakeDisplaySettingsStore store, DisplaySettings display, WorkspaceSetStore sets, FakeCloudAgentsHost cloud)
        {
            Fixture = fixture;
            Main = main;
            _directory = directory;
            About = about;
            Urls = urls;
            Store = store;
            Display = display;
            Sets = sets;
            Cloud = cloud;
        }

        public ShellFixture Fixture { get; }

        public MainView Main { get; }

        public RecordingAbout About { get; }

        public RecordingUrls Urls { get; }

        public FakeDisplaySettingsStore Store { get; }

        public DisplaySettings Display { get; }

        public WorkspaceSetStore Sets { get; }

        public FakeCloudAgentsHost Cloud { get; }

        public string StatusText => Fixture.Runtime("session-1").StatusText;

        public static async Task<Rig> CreateAsync()
        {
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(), SessionCommands = new FakeSessionCommands() });
            var directory = Path.Combine(Path.GetTempPath(), "autonogrammer-shell-slash-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var about = new RecordingAbout();
            var urls = new RecordingUrls();
            var store = new FakeDisplaySettingsStore();
            var display = new DisplaySettings(store);
            await display.LoadAsync(Token).ConfigureAwait(true);
            var sets = new WorkspaceSetStore(Path.Combine(directory, "sets.json"));
            var cloud = new FakeCloudAgentsHost(TimeProvider.System);
            var main = fixture.ShowWithServices(new ShellServices
            {
                About = about,
                UrlLauncher = urls,
                Display = display,
                WorkspaceSets = sets,
                Cloud = cloud,
                Checkpoints = new ScriptedCheckpointBackend(),
                Worktrees = new ViewWorktreeBackend(),
                NewSessionTarget = new PendingSessionTarget(),
            });
            return new Rig(fixture, main, directory, about, urls, store, display, sets, cloud);
        }

        public async Task SendAsync(string text)
        {
            var composer = Fixture.Runtime("session-1").Composer;
            composer.Draft = text;
            await composer.SendAsync().ConfigureAwait(true);
        }

        public async ValueTask DisposeAsync()
        {
            Sets.Dispose();
            await Fixture.DisposeAsync().ConfigureAwait(true);
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>An About launcher that records the windows it was asked to show over.</summary>
    private sealed class RecordingAbout : IAboutLauncher
    {
        public List<Avalonia.Controls.Window?> Owners { get; } = [];

        public void Show(Avalonia.Controls.Window? owner) => Owners.Add(owner);
    }

    /// <summary>A URL launcher that records the addresses it opened.</summary>
    private sealed class RecordingUrls : IUrlLauncher
    {
        public List<Uri> Opened { get; } = [];

        public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken)
        {
            Opened.Add(uri);
            return Task.FromResult(true);
        }
    }
}
