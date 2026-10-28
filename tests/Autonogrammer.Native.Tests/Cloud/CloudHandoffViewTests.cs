using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Cloud;
using Autonogrammer.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cloud;

/// <summary>The hand-off dialog on the headless desktop: the cost confirmation gates the start, Enter and Esc follow the rules, every control is named and big enough, and the window closes and carries its result.</summary>
public sealed class CloudHandoffViewTests
{
    private const string TaskText = "Fix the flaky test\nIt fails on CI only.";
    private const string Repository = "https://github.com/owner/app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task RunAsync(string? repository, Action<FakeCloudAgentsHost>? arrange, Func<Rig, Task> body)
    {
        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(async () =>
        {
            var rig = new Rig(repository, arrange);
            try
            {
                await body(rig).ConfigureAwait(true);
            }
            finally
            {
                rig.Window.Close();
            }
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The dialog opens with focus in the repository field, shows the task and the cost sentence, and Hand off is disabled with the reason beside it.</summary>
    [Fact]
    public async Task TheDialogOpensWithFocusInTheRepositoryFieldAndStartDisabled()
    {
        await RunAsync(null, null, rig =>
        {
            rig.Window.Title.ShouldBe(CloudCopy.HandoffTitle);
            rig.View.TaskField.Text.ShouldBe(TaskText);
            rig.View.TaskField.IsReadOnly.ShouldBeTrue();
            rig.View.CostNotice.Text.ShouldBe(CloudCopy.CostNotice(15));
            rig.View.Start.IsEnabled.ShouldBeFalse();
            rig.View.WhyDisabled.IsEffectivelyVisible.ShouldBeTrue();
            rig.View.WhyDisabled.Text.ShouldBe(CloudCopy.RepositoryRequired);
            SettingsTree.FocusedName(rig.Window).ShouldBe(CloudCopy.RepositoryEntryLabel);
            SettingsTree.Unnamed(rig.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>A suggested repository is already in the field. Start waits for the cost box, becomes enabled when it is ticked, and no host call happens before.</summary>
    [Fact]
    public async Task StartIsDisabledUntilTheRepositoryIsValidAndTheCostIsConfirmed()
    {
        await RunAsync(Repository, null, rig =>
        {
            rig.View.RepositoryField.Text.ShouldBe(Repository);
            rig.View.CostConfirm.IsChecked.ShouldNotBe(true);
            rig.View.Start.IsEnabled.ShouldBeFalse();
            rig.View.WhyDisabled.Text.ShouldBe(CloudCopy.CostNotConfirmed);

            rig.View.RepositoryField.Text = "http://github.com/owner/app";
            rig.Settle();
            rig.View.RepositoryError.IsEffectivelyVisible.ShouldBeTrue();
            rig.View.RepositoryError.Text.ShouldBe(CloudCopy.RepositoryNotHttps);

            rig.View.CostConfirm.IsChecked = true;
            rig.Settle();
            rig.View.Start.IsEnabled.ShouldBeFalse("the repository is still invalid");
            rig.Model.CostConfirmed.ShouldBeTrue("editing the repository never withdraws the confirmation");

            rig.View.RepositoryField.Text = Repository;
            rig.Settle();
            rig.View.RepositoryError.IsEffectivelyVisible.ShouldBeFalse();
            rig.View.Start.IsEnabled.ShouldBeTrue();
            rig.View.WhyDisabled.IsVisible.ShouldBeFalse();
            rig.Host.Calls.ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Enter in the repository field starts only when the model can start; otherwise it does nothing.</summary>
    [Fact]
    public async Task EnterInTheRepositoryFieldStartsOnlyWhenTheModelCanStart()
    {
        await RunAsync(Repository, null, rig =>
        {
            _ = rig.View.RepositoryField.Focus();
            SettingsTree.Press(rig.Window, Key.Enter, PhysicalKey.Enter);

            rig.Host.Calls.ShouldBeEmpty("the cost is not confirmed");
            rig.Window.IsVisible.ShouldBeTrue();

            rig.View.CostConfirm.IsChecked = true;
            rig.Settle();
            _ = rig.View.RepositoryField.Focus();
            SettingsTree.Press(rig.Window, Key.Enter, PhysicalKey.Enter);

            rig.Host.CountOf("Create").ShouldBe(1);
            rig.Closed.ShouldBe(1);
            rig.Window.Result.ShouldNotBeNull().Outcome.ShouldBe(CloudHandoffOutcome.Started);
            rig.Host.CreateRequests.ShouldHaveSingleItem().Prompt.ShouldBe(TaskText);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>The Hand off button starts the agent, closes the window and hands the result to the shell.</summary>
    [Fact]
    public async Task TheStartButtonHandsOffAndTheWindowCarriesTheResult()
    {
        await RunAsync(Repository, null, rig =>
        {
            rig.View.CostConfirm.IsChecked = true;
            rig.Settle();

            SettingsTree.Click(rig.Window, rig.View.Start);

            rig.Closed.ShouldBe(1);
            var result = rig.Window.Result.ShouldNotBeNull();
            result.Outcome.ShouldBe(CloudHandoffOutcome.Started);
            result.Agent.ShouldNotBeNull().Name.ShouldBe("Fix the flaky test");
            result.Message.ShouldBe(CloudCopy.HandoffStarted("Fix the flaky test"));
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Esc and Cancel close the window without a result and without any host call.</summary>
    [Fact]
    public async Task EscAndCancelCloseTheWindowWithoutAResult()
    {
        await RunAsync(Repository, null, rig =>
        {
            rig.View.CostConfirm.IsChecked = true;
            rig.Settle();

            SettingsTree.Press(rig.Window, Key.Escape, PhysicalKey.Escape);

            rig.Closed.ShouldBe(1);
            rig.Window.Result.ShouldBeNull();
            rig.Host.Calls.ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);

        await RunAsync(Repository, null, rig =>
        {
            SettingsTree.Click(rig.Window, rig.View.Cancel);

            rig.Closed.ShouldBe(1);
            rig.Window.Result.ShouldBeNull();
            rig.Host.Calls.ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>A refusal of the service stays on screen as an assertive error, the window stays open and Hand off can be tried again.</summary>
    [Fact]
    public async Task ARefusalStaysOnScreenAndStartCanBeTriedAgain()
    {
        await RunAsync(
            Repository,
            host => host.FailNext("Create", new CloudHostException(CloudFailureKind.QuotaExceeded, "The compute quota is used up.")),
            rig =>
            {
                rig.View.CostConfirm.IsChecked = true;
                rig.Settle();

                SettingsTree.Click(rig.Window, rig.View.Start);

                rig.Closed.ShouldBe(0);
                rig.Window.Result.ShouldBeNull();
                rig.View.Status.IsEffectivelyVisible.ShouldBeTrue();
                rig.View.Status.Text.ShouldBe("The compute quota is used up.");
                AutomationProperties.GetLiveSetting(rig.View.Status).ShouldBe(AutomationLiveSetting.Assertive);
                rig.View.Start.IsEnabled.ShouldBeTrue();

                SettingsTree.Click(rig.Window, rig.View.Start);

                rig.Closed.ShouldBe(1);
                rig.Window.Result.ShouldNotBeNull().Outcome.ShouldBe(CloudHandoffOutcome.Started);
                return Task.CompletedTask;
            }).ConfigureAwait(true);
    }

    /// <summary>Every interactive control and live region has an accessible name and a target of at least the minimum size.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnough()
    {
        await RunAsync(Repository, null, rig =>
        {
            SettingsTree.Unnamed(rig.Window).ShouldBeEmpty();
            var controls = SettingsTree.Visible<Button>(rig.View).Cast<Control>()
                .Concat(SettingsTree.Visible<TextBox>(rig.View))
                .Concat(SettingsTree.Visible<CheckBox>(rig.View))
                .Where(static control => control.TemplatedParent is null)
                .Distinct()
                .ToArray();
            controls.Length.ShouldBe(5, string.Join(", ", controls.Select(static control => control.GetType().Name + ":" + control.Name)));
            foreach (var control in controls)
            {
                var name = AutomationProperties.GetName(control);
                name.ShouldNotBeNullOrWhiteSpace();
                control.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too short");
                control.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too narrow");
            }

            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    private sealed class Rig
    {
        public Rig(string? repository, Action<FakeCloudAgentsHost>? arrange)
        {
            Host = new FakeCloudAgentsHost(new ManualTimeProvider());
            arrange?.Invoke(Host);
            Model = new CloudHandoffModel(new CloudHandoff(Host), new CloudSetup { IsConfigured = true }, TaskText, repository);
            Window = new CloudHandoffWindow(Model);
            Window.Closed += (_, _) => Closed++;
            Window.Show();
            Settle();
        }

        public FakeCloudAgentsHost Host { get; }

        public CloudHandoffModel Model { get; }

        public CloudHandoffWindow Window { get; }

        public CloudHandoffView View => Window.View;

        public int Closed { get; private set; }

        public void Settle() => SettingsTree.SettleSafe(Window);
    }
}
