using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Presentation.Cloud;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Cloud;

/// <summary>The Cloud agents page: one state at a time, every status drawn in words and a glyph, the details and their commands, and the keyboard and accessibility contract.</summary>
public sealed class CloudAgentsPageViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void ThreeAgents(FakeCloudAgentsHost host)
    {
        host.Add(CloudAgents.Agent("a", CloudAgentStatus.Running, "Running agent"));
        host.Add(CloudAgents.Agent("b", CloudAgentStatus.Stopped, "Stopped agent"));
        host.Add(CloudAgents.Agent("c", CloudAgentStatus.Failed, "Failed agent"));
    }

    private static string[] ButtonNames(Avalonia.Visual root) =>
        [.. SettingsTree.Visible<Button>(root).Where(static button => button.TemplatedParent is null).Select(static button => AutomationProperties.GetName(button) ?? string.Empty)];

    /// <summary>While the first load runs the page shows only the loading text: no toolbar, no list, no setup steps.</summary>
    [Fact]
    public async Task WhileLoadingOnlyTheLoadingTextShows()
    {
        await CloudScreen.RunAsync(static host => _ = host.HoldNext("Setup"), screen =>
        {
            var view = screen.View;

            view.Loading.IsEffectivelyVisible.ShouldBeTrue();
            view.NotConfigured.IsVisible.ShouldBeFalse();
            view.LoadFailed.IsVisible.ShouldBeFalse();
            view.Empty.IsVisible.ShouldBeFalse();
            view.Ready.IsVisible.ShouldBeFalse();
            view.NewAgent.IsEffectivelyVisible.ShouldBeFalse();
            ButtonNames(view).ShouldBeEmpty();
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// A service that is not set up shows the reason and the numbered setup steps, one Check again button, and no control that acts on
    /// an agent; Check again reads the setup again and moves on once it is set up.
    /// </summary>
    [Fact]
    public async Task NotSetUpShowsTheStepsAndNoAgentControls()
    {
        await CloudScreen.RunAsync(static host => host.Setup = CloudSetup.NotConfigured("You are not signed in to the Autonogrammer service."), screen =>
        {
            var view = screen.View;
            var texts = SettingsTree.Texts(view);

            view.NotConfigured.IsEffectivelyVisible.ShouldBeTrue();
            view.Ready.IsVisible.ShouldBeFalse();
            view.NewAgent.IsEffectivelyVisible.ShouldBeFalse();
            view.Refresh.IsEffectivelyVisible.ShouldBeFalse();
            view.StatusFilter.IsEffectivelyVisible.ShouldBeFalse();
            view.Agents.IsEffectivelyVisible.ShouldBeFalse();
            texts.ShouldContain(CloudCopy.NotConfiguredHeading, StringComparer.Ordinal);
            texts.ShouldContain("You are not signed in to the Autonogrammer service.", StringComparer.Ordinal);
            for (var index = 0; index < CloudCopy.SetupSteps.Count; index++)
            {
                texts.ShouldContain(CloudCopy.SetupStepLine(index + 1, CloudCopy.SetupSteps[index]), StringComparer.Ordinal);
            }

            ButtonNames(view).ShouldBe([CloudCopy.CheckAgainLabel]);
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            screen.Host.CountOf("List").ShouldBe(0);

            screen.Host.Setup = new CloudSetup { IsConfigured = true };
            screen.Click(view.CheckAgain);

            screen.Model.State.ShouldBe(CloudAgentsViewState.Empty);
            view.NotConfigured.IsVisible.ShouldBeFalse();
            view.Empty.IsEffectivelyVisible.ShouldBeTrue();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A failed first load shows the reason and Try again; pressing it loads the list.</summary>
    [Fact]
    public async Task AFailedLoadShowsTheReasonAndTryAgainLoadsTheList()
    {
        await CloudScreen.RunAsync(
            static host =>
            {
                ThreeAgents(host);
                host.FailNext("List", new CloudHostException("The service did not answer."));
            },
            screen =>
            {
                var view = screen.View;
                var texts = SettingsTree.Texts(view);

                view.LoadFailed.IsEffectivelyVisible.ShouldBeTrue();
                texts.ShouldContain(CloudCopy.LoadFailedHeading, StringComparer.Ordinal);
                texts.ShouldContain("The service did not answer.", StringComparer.Ordinal);
                ButtonNames(view).ShouldBe([CloudCopy.TryAgainLabel]);
                SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();

                screen.Click(view.TryAgain);

                view.LoadFailed.IsVisible.ShouldBeFalse();
                view.Ready.IsEffectivelyVisible.ShouldBeTrue();
                view.Agents.ItemCount.ShouldBe(3);
                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }

    /// <summary>With no agent the page says so and offers New cloud agent; there is no list and no filter.</summary>
    [Fact]
    public async Task NoAgentShowsTheEmptyStateWithNewCloudAgent()
    {
        await CloudScreen.RunAsync(null, screen =>
        {
            var view = screen.View;
            var texts = SettingsTree.Texts(view);

            view.Empty.IsEffectivelyVisible.ShouldBeTrue();
            texts.ShouldContain(CloudCopy.EmptyHeading, StringComparer.Ordinal);
            texts.ShouldContain(CloudCopy.EmptyBody, StringComparer.Ordinal);
            view.NewAgent.IsEffectivelyVisible.ShouldBeTrue();
            view.NewAgent.IsEnabled.ShouldBeTrue();
            view.Agents.IsEffectivelyVisible.ShouldBeFalse();
            view.StatusFilter.IsEffectivelyVisible.ShouldBeFalse();
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every row shows its name, its status in words and its accessible name; nothing is selected, so the details wait for a choice.</summary>
    [Fact]
    public async Task TheListShowsEveryAgentWithItsStatusInWords()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            var texts = SettingsTree.Texts(view);
            var names = SettingsTree.Descriptions(view);

            view.Agents.ItemCount.ShouldBe(3);
            foreach (var row in screen.Model.Rows)
            {
                texts.ShouldContain(row.Name, StringComparer.Ordinal);
                texts.ShouldContain(row.StatusText, StringComparer.Ordinal);
                names.ShouldContain(row.AccessibleName, StringComparer.Ordinal);
            }

            view.Details.IsVisible.ShouldBeFalse();
            texts.ShouldContain(CloudCopy.NoSelection, StringComparer.Ordinal);
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Selecting a stopped agent shows its facts, its history and a pull request line, with Wake offered and Stop not.</summary>
    [Fact]
    public async Task SelectingAnAgentShowsItsDetails()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;

            screen.SelectRow("b");
            var texts = SettingsTree.Texts(view);

            view.Details.IsEffectivelyVisible.ShouldBeTrue();
            texts.ShouldContain("Stopped agent", StringComparer.Ordinal);
            texts.ShouldContain(CloudCopy.DescribeStopped, StringComparer.Ordinal);
            texts.ShouldContain("Branch: autonogrammer/b", StringComparer.Ordinal);
            texts.ShouldContain("Starts from main", StringComparer.Ordinal);
            texts.ShouldContain("Environment: Production", StringComparer.Ordinal);
            texts.ShouldContain("owner/app", StringComparer.Ordinal);
            view.Timeline.ItemCount.ShouldBe(2);
            view.PullRequests.ItemCount.ShouldBe(1);
            screen.ButtonNamed(CloudCopy.CreatePullRequestName("owner/app")).IsEnabled.ShouldBeTrue();
            view.Wake.IsEffectivelyVisible.ShouldBeTrue();
            view.Wake.IsEnabled.ShouldBeTrue();
            view.Stop.IsVisible.ShouldBeFalse();
            view.Delete.IsEnabled.ShouldBeTrue();
            view.OpenInAgents.IsEnabled.ShouldBeTrue();
            view.Prompt.IsEnabled.ShouldBeFalse();
            view.Send.IsEnabled.ShouldBeFalse();
            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Wake and Stop are offered only to the statuses they fit; Delete is offered to all but a deleting agent; Open in Agents needs a status that has a session.</summary>
    [Theory]
    [InlineData(CloudAgentStatus.Provisioning, false, false, true, false, false)]
    [InlineData(CloudAgentStatus.Running, false, true, true, true, true)]
    [InlineData(CloudAgentStatus.Stopped, true, false, true, false, true)]
    [InlineData(CloudAgentStatus.Orphaned, false, false, true, false, false)]
    [InlineData(CloudAgentStatus.LimitReached, false, false, true, false, true)]
    [InlineData(CloudAgentStatus.Deleting, false, false, false, false, false)]
    public async Task TheDetailButtonsFollowTheStatus(CloudAgentStatus status, bool wake, bool stop, bool delete, bool prompt, bool open)
    {
        await CloudScreen.RunAsync(host => host.Add(CloudAgents.Agent("x", status, "Only agent")), screen =>
        {
            var view = screen.View;

            screen.SelectRow("x");

            view.Wake.IsVisible.ShouldBe(wake);
            view.Stop.IsVisible.ShouldBe(stop);
            view.Delete.IsEnabled.ShouldBe(delete);
            view.Prompt.IsEnabled.ShouldBe(prompt);
            view.OpenInAgents.IsEnabled.ShouldBe(open);
            SettingsTree.Texts(view).ShouldContain(CloudAgentStatusMap.DescriptionOf(status), StringComparer.Ordinal);
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Wake calls the host, announces it politely, and the row and the buttons follow the new status.</summary>
    [Fact]
    public async Task WakeWakesTheAgentAndAnnouncesIt()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("b");

            screen.Click(screen.ButtonNamed(CloudCopy.WakeName("Stopped agent")));

            screen.Host.Calls.ShouldContain("Wake:b", StringComparer.Ordinal);
            view.Notice.Text.ShouldBe(CloudCopy.WakeRequested("Stopped agent"));
            view.Notice.IsVisible.ShouldBeTrue();
            AutomationProperties.GetLiveSetting(view.Notice).ShouldBe(AutomationLiveSetting.Polite);
            view.Wake.IsVisible.ShouldBeFalse();
            view.Stop.IsVisible.ShouldBeTrue();
            screen.Model.Rows.Single(row => string.Equals(row.Id, "b", StringComparison.Ordinal)).StatusText.ShouldBe(CloudCopy.StatusStarting);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A refused command shows the service's words in an assertive message and keeps the buttons usable.</summary>
    [Fact]
    public async Task ARefusedCommandShowsItsReason()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("b");
            screen.Host.FailNext("Wake", new CloudHostException(CloudFailureKind.LimitReached, "Too many workers are awake."));

            screen.Click(view.Wake);

            view.ActionError.IsVisible.ShouldBeTrue();
            view.ActionError.Text.ShouldBe("Too many workers are awake.");
            AutomationProperties.GetLiveSetting(view.ActionError).ShouldBe(AutomationLiveSetting.Assertive);
            view.Wake.IsEnabled.ShouldBeTrue();
            view.Notice.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Delete opens a confirmation that names what is removed and puts focus on Cancel; Cancel closes it and gives focus back to Delete; nothing is deleted.</summary>
    [Fact]
    public async Task DeleteAsksFirstAndCancelKeepsTheAgent()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("b");

            screen.Click(view.Delete);

            view.DeleteConfirmation.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain(CloudCopy.DeleteWarning("Stopped agent"), StringComparer.Ordinal);
            screen.FocusedName().ShouldBe(CloudCopy.CancelLabel);
            screen.Host.CountOf("Delete").ShouldBe(0);
            view.Delete.IsEnabled.ShouldBeFalse();
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();

            screen.Click(view.CancelDelete);

            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            screen.FocusedName().ShouldBe(CloudCopy.DeleteName("Stopped agent"));
            screen.Host.Agents.Count.ShouldBe(3);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Confirming deletes the agent: it leaves the list, the details go, and the announcement is not lost with them.</summary>
    [Fact]
    public async Task ConfirmingDeleteRemovesTheAgentFromTheList()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("b");
            screen.Click(view.Delete);

            screen.Click(view.ConfirmDelete);

            screen.Host.Calls.ShouldContain("Delete:b", StringComparer.Ordinal);
            screen.Host.Agents.Select(agent => agent.Id).ShouldBe(["a", "c"]);
            view.Agents.ItemCount.ShouldBe(2);
            view.Details.IsVisible.ShouldBeFalse();
            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Typing a message enables Send; Send delivers the trimmed text and clears the box.</summary>
    [Fact]
    public async Task SendDeliversTheMessage()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("a");
            view.Send.IsEnabled.ShouldBeFalse();

            view.Prompt.Text = "  Run the tests.  ";
            screen.Settle();
            view.Send.IsEnabled.ShouldBeTrue();
            screen.Click(view.Send);

            screen.Host.Prompts.ShouldBe(["a: Run the tests."]);
            view.Prompt.Text.ShouldBeNullOrEmpty();
            view.Send.IsEnabled.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>An agent without a pull request offers to create one; afterwards the same line offers to open it.</summary>
    [Fact]
    public async Task APullRequestIsCreatedThenOpened()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            screen.SelectRow("a");

            screen.Click(screen.ButtonNamed(CloudCopy.CreatePullRequestName("owner/app")));

            screen.Host.PullRequestRequests.ShouldHaveSingleItem().Draft.ShouldBeTrue();
            screen.Host.OpenedPullRequests.Count.ShouldBe(1);
            SettingsTree.Has<Button>(screen.View, CloudCopy.CreatePullRequestName("owner/app")).ShouldBeFalse();

            screen.Click(screen.ButtonNamed(CloudCopy.OpenPullRequestName("owner/app")));

            screen.Host.PullRequestRequests.Count.ShouldBe(1, "an existing pull request is opened, not created again");
            screen.Host.OpenedPullRequests.Count.ShouldBe(2);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Open in Agents hands the agent's session to the owner of the page.</summary>
    [Fact]
    public async Task OpenInAgentsHandsOverTheSession()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            CloudAgentOpenEventArgs? opened = null;
            screen.Model.OpenInAgentsRequested += (_, args) => opened = args;
            screen.SelectRow("a");

            screen.Click(screen.View.OpenInAgents);

            opened.ShouldNotBeNull().SessionId.ShouldBe("session-a");
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The filter keeps one status, and a status nobody has says so.</summary>
    [Fact]
    public async Task TheFilterKeepsOneStatus()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;

            view.StatusFilter.SelectedItem = screen.Model.FilterOptions.Single(option => option.Status == CloudAgentStatus.Failed);
            screen.Settle();

            view.Agents.ItemCount.ShouldBe(1);
            SettingsTree.Texts(view).ShouldContain("Failed agent", StringComparer.Ordinal);

            view.StatusFilter.SelectedItem = screen.Model.FilterOptions.Single(option => option.Status == CloudAgentStatus.Orphaned);
            screen.Settle();

            view.Agents.ItemCount.ShouldBe(0);
            SettingsTree.Texts(view).ShouldContain(CloudCopy.NoMatches("Orphaned"), StringComparer.Ordinal);

            view.StatusFilter.SelectedItem = screen.Model.FilterOptions[0];
            screen.Settle();

            view.Agents.ItemCount.ShouldBe(3);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>When the host reports a change the list follows without a click.</summary>
    [Fact]
    public async Task AHostChangeUpdatesTheList()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            screen.Host.Add(CloudAgents.Agent("d", CloudAgentStatus.Provisioning, "New agent"));

            screen.Host.PublishChanges();
            screen.Settle();

            screen.View.Agents.ItemCount.ShouldBe(4);
            SettingsTree.Texts(screen.View).ShouldContain(CloudCopy.StatusProvisioning, StringComparer.Ordinal);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A failed refresh keeps the list and shows the reason in an assertive banner.</summary>
    [Fact]
    public async Task AFailedRefreshKeepsTheListAndShowsABanner()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.Host.FailNext("List", new CloudHostException("The service did not answer."));

            screen.Click(view.Refresh);

            view.RefreshError.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain("The service did not answer.", StringComparer.Ordinal);
            view.Agents.ItemCount.ShouldBe(3);
            AutomationProperties.GetLiveSetting(view.RefreshError).ShouldBe(AutomationLiveSetting.Assertive);

            screen.Click(view.Refresh);

            view.RefreshError.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A reached agent cap disables New cloud agent and says why on the page.</summary>
    [Fact]
    public async Task AReachedCapDisablesNewCloudAgentAndSaysWhy()
    {
        await CloudScreen.RunAsync(
            static host =>
            {
                ThreeAgents(host);
                host.MaxAgents = 3;
            },
            screen =>
            {
                screen.View.NewAgent.IsEnabled.ShouldBeFalse();
                SettingsTree.Texts(screen.View).ShouldContain(CloudCopy.AgentCapReached(3), StringComparer.Ordinal);
                SettingsTree.Texts(screen.View).ShouldContain("3 of 3 agents, 1 awake", StringComparer.Ordinal);
                return Task.CompletedTask;
            },
            Token).ConfigureAwait(true);
    }

    /// <summary>With an agent selected and a delete confirmation open every control has an accessible name, and every control meets the target size.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnough()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("a");
            view.Prompt.Text = "Hello";
            screen.Settle();
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();
            screen.Click(view.Delete);
            SettingsTree.Unnamed(screen.Window).ShouldBeEmpty();

            var controls = SettingsTree.Visible<Button>(view).Cast<Control>()
                .Concat(SettingsTree.Visible<TextBox>(view))
                .Concat(SettingsTree.Visible<ComboBox>(view))
                .Where(static control => control.TemplatedParent is null)
                .ToArray();
            controls.Length.ShouldBeGreaterThan(6);
            foreach (var control in controls)
            {
                var name = AutomationProperties.GetName(control);
                control.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too short");
                control.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too narrow");
            }

            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Tab walks the toolbar and then the details in the order they are drawn; Send is skipped while it is disabled.</summary>
    [Fact]
    public async Task TabFollowsTheOrderOfTheControls()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            var view = screen.View;
            screen.SelectRow("a");

            var order = SettingsTree.TabStops(view)
                .Where(static control => control is Button or TextBox or ComboBox)
                .Select(static control => AutomationProperties.GetName(control))
                .ToArray();

            order.ShouldBe(
            [
                CloudCopy.NewAgentLabel,
                CloudCopy.RefreshLabel,
                CloudCopy.FilterName,
                CloudCopy.CreatePullRequestName("owner/app"),
                CloudCopy.StopName("Running agent"),
                CloudCopy.OpenInAgentsName("Running agent"),
                CloudCopy.DeleteName("Running agent"),
                CloudCopy.PromptName("Running agent"),
            ]);

            _ = view.NewAgent.Focus();
            SettingsTree.PressTab(screen.Window);
            screen.FocusedName().ShouldBe(CloudCopy.RefreshLabel);
            SettingsTree.PressTab(screen.Window);
            screen.FocusedName().ShouldBe(CloudCopy.FilterName);
            SettingsTree.PressTab(screen.Window, backwards: true);
            screen.FocusedName().ShouldBe(CloudCopy.RefreshLabel);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Enter in the message box sends the message.</summary>
    [Fact]
    public async Task EnterInTheMessageBoxSends()
    {
        await CloudScreen.RunAsync(ThreeAgents, screen =>
        {
            screen.SelectRow("a");
            screen.View.Prompt.Text = "Run the tests.";
            screen.Settle();
            _ = screen.View.Prompt.Focus();

            SettingsTree.Press(screen.Window, Key.Enter, PhysicalKey.Enter);

            screen.Host.Prompts.ShouldBe(["a: Run the tests."]);
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }
}
