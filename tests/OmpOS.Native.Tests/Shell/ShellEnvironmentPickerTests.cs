using Avalonia.Controls;
using OmpOS.Native.Tests.AgentPanel.Shell;
using OmpOS.Native.Tests.Remote;
using OmpOS.Native.Views.Remote;
using OmpOS.Native.Views.Shell;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Shell;

/// <summary>The environment picker of the rail: Local and the saved ssh environments, the next session starting where the picker says, and a blocked choice stopping New agent.</summary>
public sealed class ShellEnvironmentPickerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The picker lists this computer and each saved ssh environment, and exists only when the head supplies a remote environments host.</summary>
    [Fact]
    public async Task ThePickerListsLocalAndTheSavedEnvironments()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var host = new ScriptedRemoteHost().With("box-1", "alpha").With("box-2", "beta");
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = host, NewSessionTarget = new PendingSessionTarget() });
            await Task.Yield();
            ShellHarness.Settle(fixture.ServicesWindow!);

            main.FindControl<Expander>("EnvironmentExpander")!.IsVisible.ShouldBeTrue();
            var model = main.TargetModel.ShouldNotBeNull();
            model.Options.Select(static option => option.Id).ShouldBe([NewSessionTargetModel.LocalId, "ssh:box-1", "ssh:box-2"]);
            model.Selected!.Id.ShouldBe(NewSessionTargetModel.LocalId, Case.Sensitive);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Choosing an ssh environment makes it the pending target of the next session; choosing Local clears it.</summary>
    [Fact]
    public async Task TheChosenEnvironmentIsThePendingTarget()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var pending = new PendingSessionTarget();
            var host = new ScriptedRemoteHost().With("box-1", "alpha");
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = host, NewSessionTarget = pending });
            await Task.Yield();
            var model = main.TargetModel.ShouldNotBeNull();

            model.Select("ssh:box-1");
            var chosen = pending.Take().ShouldNotBeNull();
            chosen.Kind.ShouldBe(SessionTargetKind.Ssh);
            chosen.ProfileId.ShouldBe("box-1", Case.Sensitive);

            model.Select("ssh:box-1");
            model.Select(NewSessionTargetModel.LocalId);
            pending.Take().ShouldBeNull();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Starting a session sets the target again, so the picker's choice also governs the sessions after it.</summary>
    [Fact]
    public async Task TheChoiceSurvivesTheSessionItStarted()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var pending = new PendingSessionTarget();
            var host = new ScriptedRemoteHost().With("box-1", "alpha");
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = host, NewSessionTarget = pending });
            await Task.Yield();
            main.TargetModel!.Select("ssh:box-1");
            pending.Take().ShouldNotBeNull("the engine set takes the target when it creates a session");

            ShellHarness.RaiseClick(ShellTree.Named<Button>(main, "NewAgentButton")!);
            await Task.Yield();

            pending.Take().ShouldNotBeNull("the picker still says ssh, so the next session starts there too");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The picker's manage link asks for the Remote environments page of Settings.</summary>
    [Fact]
    public async Task ManageOpensTheRemoteSettingsPage()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = new ScriptedRemoteHost(), NewSessionTarget = new PendingSessionTarget() });
            var seen = new List<SettingsSection?>();
            main.SettingsRequested += (_, e) => seen.Add(e.Section);
            var expander = main.FindControl<Expander>("EnvironmentExpander")!;
            expander.IsExpanded = true;
            ShellHarness.Settle(fixture.ServicesWindow!);

            var picker = (EnvironmentPickerView)main.FindControl<ContentControl>("EnvironmentHost")!.Content!;
            ShellHarness.RaiseClick(picker.Manage);

            seen.ShouldBe([SettingsSection.Remote]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A choice that cannot start (an environment whose last check failed to sign in) disables New agent, says why, and Ctrl+N starts nothing.</summary>
    [Fact]
    public async Task ABlockedChoiceStopsNewAgent()
    {
        await using var headless = TestAppBuilder.StartSession();
        await headless.RunAsync(async () =>
        {
            await using var fixture = ShellFixture.CreateWithSession();
            var pending = new PendingSessionTarget();
            var host = new ScriptedRemoteHost().With("box-1", "alpha");
            var main = fixture.ShowWithServices(new ShellServices { RemoteEnvironments = host, NewSessionTarget = pending });
            await Task.Yield();
            var model = main.TargetModel!;
            var button = ShellTree.Named<Button>(main, "NewAgentButton")!;
            button.IsEnabled.ShouldBeTrue();

            model.Select("ssh:box-1");
            model.SetStatus("box-1", RemoteEnvironmentStatus.AuthFailed);
            ShellHarness.Settle(fixture.ServicesWindow!);

            model.CanStart.ShouldBeFalse();
            button.IsEnabled.ShouldBeFalse();
            pending.Take().ShouldBeNull("a blocked choice is never the pending target");

            model.Select(NewSessionTargetModel.LocalId);
            button.IsEnabled.ShouldBeTrue();
        }, Token).ConfigureAwait(true);
    }
}
