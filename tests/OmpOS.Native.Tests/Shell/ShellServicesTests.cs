using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.Parity;
using OmpOS.Native.Tests.SlashPalette;
using OmpOS.Native.Views;
using OmpOS.Native.Views.Shell;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Settings;
using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shell;

/// <summary>The window-level wiring of the shell: About, the conversation search overlay and the Settings route.</summary>
public sealed class ShellServicesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The About button of the rail footer opens the head's About launcher over the window, comes after Settings in the tab order, and is named and sized.</summary>
    [Fact]
    public async Task TheAboutButtonOpensTheAboutLauncherAndFollowsSettings()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var about = new RecordingAboutLauncher();
            var main = fixture.ShowWithServices(new ShellServices { About = about });
            var button = main.FindControl<Button>("AboutButton")!;
            var settings = main.FindControl<Button>("SettingsButton")!;

            AutomationProperties.GetName(button).ShouldBe("About OmpOS");
            button.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            button.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            ((Panel)settings.Parent!).Children.IndexOf(button).ShouldBeGreaterThan(((Panel)settings.Parent!).Children.IndexOf(settings));

            ShellHarness.RaiseClick(button);

            about.Owners.Count.ShouldBe(1);
            about.Owners[0].ShouldBeSameAs(fixture.ServicesWindow);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Ctrl+K opens the conversation search over the window and Ctrl+K again closes it; Cmd+K (Meta) does the same.</summary>
    [Theory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Meta)]
    public async Task TheSearchShortcutTogglesTheConversationSearch(KeyModifiers modifier)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var model = main.ConversationSearch.ShouldNotBeNull();
            model.IsOpen.ShouldBeFalse();

            Press(fixture, Key.K, PhysicalKey.K, modifier);
            model.IsOpen.ShouldBeTrue();

            Press(fixture, Key.K, PhysicalKey.K, modifier);
            model.IsOpen.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Ctrl+F stays the find in the open conversation: it shows the transcript search and leaves the conversation search closed.</summary>
    [Fact]
    public async Task TheFindShortcutStaysTheTranscriptSearch()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());

            Press(fixture, Key.F, PhysicalKey.F, KeyModifiers.Control);

            main.FindControl<ContentControl>("SearchHost")!.IsVisible.ShouldBeTrue();
            main.ConversationSearch!.IsOpen.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Enter on a result of the conversation search selects that session and closes the overlay.</summary>
    [Fact]
    public async Task ChoosingASearchResultSelectsTheSessionAndClosesTheOverlay()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            fixture.AddSession("session-2", "Second agent");
            var main = fixture.ShowWithServices(new ShellServices());
            fixture.Panel.SelectSession("session-1");
            ShellHarness.Settle(fixture.ServicesWindow!);

            Press(fixture, Key.K, PhysicalKey.K, KeyModifiers.Control);
            var model = main.ConversationSearch!;
            model.SetQuery("Second");
            ShellHarness.Settle(fixture.ServicesWindow!);
            model.Results.Count.ShouldBeGreaterThan(0);
            var view = (ConversationSearchView)main.FindControl<ContentControl>("ConversationSearchHost")!.Content!;
            view.Query.Focus();

            Press(fixture, Key.Enter, PhysicalKey.Enter, KeyModifiers.None);

            fixture.Panel.Selected!.Id.ShouldBe("session-2");
            model.IsOpen.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The rail Settings button asks for the first page, and a request that names a page carries it.</summary>
    [Fact]
    public async Task SettingsRequestsCarryTheirPage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);

            ShellHarness.RaiseClick(main.FindControl<Button>("SettingsButton")!);
            main.RequestSettings(SettingsSection.Hooks);

            seen.ShouldBe([null, SettingsSection.Hooks]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Each slash command that names a Settings page opens that page.</summary>
    [Theory]
    [InlineData("/rules", SettingsSection.Rules)]
    [InlineData("/skills", SettingsSection.Skills)]
    [InlineData("/commands", SettingsSection.Commands)]
    [InlineData("/plugins", SettingsSection.Plugins)]
    [InlineData("/bedrock", SettingsSection.Models)]
    [InlineData("/run-everything", SettingsSection.RunMode)]
    [InlineData("/auto-run", SettingsSection.RunMode)]
    public async Task ASettingsSlashCommandOpensItsPage(string command, SettingsSection expected)
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession(new SessionScript { Prompts = new ScriptedPromptChannel(), SessionCommands = new FakeSessionCommands() });
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);
            var composer = fixture.Runtime("session-1").Composer;

            composer.Draft = command;
            await composer.SendAsync().ConfigureAwait(true);

            seen.ShouldBe([expected]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Without a services record the window still builds, and the About button falls back to this build's own launcher.</summary>
    [Fact]
    public async Task AWindowWithNoServicesStillBuilds()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices());

            main.FindControl<Button>("AboutButton")!.IsVisible.ShouldBeTrue();
            main.FindControl<Expander>("EnvironmentExpander")!.IsVisible.ShouldBeFalse("no remote environments host, no picker");
            main.FindControl<ToggleButton>("WorktreesToggle")!.IsVisible.ShouldBeFalse();
            main.FindControl<ToggleButton>("HookLogToggle")!.IsVisible.ShouldBeFalse();
            main.FindControl<ToggleButton>("CheckpointsToggle")!.IsVisible.ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The workspace button opens the workspace dialog over the session's folder; Start saves the set and starts a session, and a dialog that was cancelled does neither.</summary>
    [Fact]
    public async Task TheWorkspaceDialogSavesTheSetAndStartsASession()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ompos-shell-sets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var headless = TestAppBuilder.StartSession();
            await headless.RunAsync(async () =>
            {
                var fixture = ShellFixture.CreateWithSession();
                await using var fixtureScope = fixture.ConfigureAwait(true);
                using var store = new WorkspaceSetStore(Path.Combine(directory, "sets.json"));
                var main = fixture.ShowWithServices(new ShellServices { WorkspacePicker = new NoIssuesBackend(), WorkspaceSets = store, NewSessionTarget = new PendingSessionTarget() });
                var button = main.FindControl<Button>("WorkspacesButton")!;
                IReadOnlyList<WorkspacePickerRoot>? shown = null;
                var start = false;
                main.WorkspaceDialog = (picker, _) =>
                {
                    shown = picker.Snapshot();
                    picker.AddFolder("/work/lib");
                    return Task.FromResult(start);
                };
                AutomationProperties.GetName(button).ShouldBe("Workspace sets");
                button.IsVisible.ShouldBeTrue();
                var sessions = fixture.Host.Runtimes.Count;

                ShellHarness.RaiseClick(button);
                await main.WorkspaceDialogTask.ConfigureAwait(true);

                shown.ShouldNotBeNull().Count.ShouldBe(1, "the dialog opens over the session's own folder");
                (await store.ListAsync(Token).ConfigureAwait(true)).ShouldBeEmpty("a cancelled dialog saves nothing");
                fixture.Host.Runtimes.Count.ShouldBe(sessions);

                start = true;
                ShellHarness.RaiseClick(button);
                await main.WorkspaceDialogTask.ConfigureAwait(true);

                var saved = (await store.ListAsync(Token).ConfigureAwait(true)).ShouldHaveSingleItem();
                saved.Set.Roots.Count.ShouldBe(2);
                saved.Set.Roots[1].Path.ShouldBe("/work/lib");
                fixture.Host.Runtimes.Count.ShouldBeGreaterThan(sessions);
            }, Token).ConfigureAwait(true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Every new control of the shell has an accessible name and a target of at least the minimum size.</summary>
    [Fact]
    public async Task TheNewShellControlsAreNamedAndSized()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            var fixture = ShellFixture.CreateWithSession();
            await using var fixtureScope = fixture.ConfigureAwait(true);
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = new OmpOS.Native.Tests.Remote.ScriptedRemoteHost(), NewSessionTarget = new PendingSessionTarget() });

            (string Name, string Accessible)[] controls =
            [
                ("AboutButton", "About OmpOS"),
                ("WorkspacesButton", "Workspace sets"),
                ("CheckpointsToggle", "Checkpoints"),
                ("WorktreesToggle", "Worktrees"),
                ("HookLogToggle", "Hook run log"),
            ];
            foreach (var (name, accessible) in controls)
            {
                var control = main.FindControl<Control>(name).ShouldNotBeNull(name);
                AutomationProperties.GetName(control).ShouldBe(accessible, name);
            }

            main.FindControl<Button>("AboutButton")!.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            main.FindControl<Button>("WorkspacesButton")!.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            AutomationProperties.GetName(main.FindControl<Expander>("EnvironmentExpander")!).ShouldStartWith(RemoteCopy.PickerHeading, Case.Sensitive);
        }, Token).ConfigureAwait(true);
    }

    private static void Press(ShellFixture fixture, Key key, PhysicalKey physical, KeyModifiers modifiers)
    {
        var window = fixture.ServicesWindow!;
        window.KeyPress(key, (RawInputModifiers)modifiers, physical, null);
        ShellHarness.Settle(window);
    }

    /// <summary>An About launcher that records the windows it was asked to show over.</summary>
    private sealed class RecordingAboutLauncher : IAboutLauncher
    {
        public List<Window?> Owners { get; } = [];

        public void Show(Window? owner) => Owners.Add(owner);
    }

    /// <summary>A workspace dialog backend that finds no problem with any folder and imports nothing.</summary>
    private sealed class NoIssuesBackend : IWorkspaceSetPickerBackend
    {
        public IReadOnlyList<WorkspacePickerIssue> Validate(IReadOnlyList<WorkspacePickerRoot> roots) => [];

        public WorkspacePickerImport ImportCodeWorkspace(string filePath) => throw new NotSupportedException();

        public void ExportCodeWorkspace(string filePath, IReadOnlyList<WorkspacePickerRoot> roots) => throw new NotSupportedException();
    }
}
