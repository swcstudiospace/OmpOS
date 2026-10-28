using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>The Cloud agents page model: its states, the list with its status words, the filter, the selection, the refresh on host changes and the errors with retry.</summary>
public sealed class CloudAgentsViewModelTests
{
    private const string Canary = "s3cr3t-canary-value";

    private readonly ManualTimeProvider _clock = new();

    /// <summary>The first load shows Loading until the list arrives, then Ready with a row per agent and the usage line.</summary>
    [Fact]
    public async Task TheFirstLoadShowsLoadingThenTheList()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running), CloudFixtures.Agent("b", CloudAgentStatus.Idle));
        host.MaxAgents = 5;
        host.MaxAwakeAgents = 2;
        var release = host.HoldNext("List");
        using var model = new CloudAgentsViewModel(host, _clock);

        var pending = model.LoadAsync(TestContext.Current.CancellationToken);

        model.State.ShouldBe(CloudAgentsViewState.Loading);
        model.Rows.ShouldBeEmpty();
        release();
        await pending.ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Ready);
        model.Rows.Count.ShouldBe(2);
        model.TotalCount.ShouldBe(2);
        model.LimitsText.ShouldBe("2 of 5 agents, 2 of 2 awake");
        model.CanCreate.ShouldBeTrue();
        model.LoadError.ShouldBeEmpty();
    }

    /// <summary>With no agent the page is empty and still offers New cloud agent.</summary>
    [Fact]
    public async Task NoAgentIsAnEmptyStateThatStillOffersCreate()
    {
        using var model = await CloudFixtures.LoadedAsync(CloudFixtures.Host(_clock), _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Empty);
        model.Rows.ShouldBeEmpty();
        model.CanCreate.ShouldBeTrue();
        model.BeginNewAgent().ShouldNotBeNull();
    }

    /// <summary>A host that is not configured ends in the setup state with its reason, lists nothing and offers no create.</summary>
    [Fact]
    public async Task ANotConfiguredHostShowsSetupAndNothingToActOn()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        host.Setup = CloudSetup.NotConfigured("You are not signed in to the Autonogrammer service.");
        using var model = new CloudAgentsViewModel(host, _clock);

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.NotConfigured);
        model.NotConfiguredReason.ShouldBe("You are not signed in to the Autonogrammer service.");
        model.Rows.ShouldBeEmpty();
        model.CanCreate.ShouldBeFalse();
        model.BeginNewAgent().ShouldBeNull();
        host.CountOf("List").ShouldBe(0, "no agent is listed before cloud is set up");
        host.CountOf("Limits").ShouldBe(0);
    }

    /// <summary>Without a reason from the host the setup state says what cloud agents need.</summary>
    [Fact]
    public async Task ANotConfiguredHostWithoutAReasonUsesTheDefaultSentence()
    {
        var host = CloudFixtures.Host(_clock);
        host.Setup = CloudSetup.NotConfigured();
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.NotConfiguredReason.ShouldBe(CloudCopy.NotConfiguredIntro);
    }

    /// <summary>The host may also say it is not configured while listing; that ends in the setup state, not in an error.</summary>
    [Fact]
    public async Task ANotConfiguredFailureWhileListingShowsSetup()
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("List", new CloudHostException(CloudFailureKind.NotConfigured, "Cloud agents are switched off."));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.NotConfigured);
        model.NotConfiguredReason.ShouldBe("Cloud agents are switched off.");
    }

    /// <summary>A failed first load is an error with the host's reason, and loading again recovers.</summary>
    [Fact]
    public async Task AFailedLoadShowsTheReasonAndRetryRecovers()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle));
        host.FailNext("List", new CloudHostException(CloudFailureKind.Unavailable, "The service did not answer."));
        using var model = new CloudAgentsViewModel(host, _clock);

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Error);
        model.LoadError.ShouldBe("The service did not answer.");
        model.CanCreate.ShouldBeFalse();

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Ready);
        model.LoadError.ShouldBeEmpty();
        model.Rows.Count.ShouldBe(1);
    }

    /// <summary>An exception the host did not translate reads as the plain failure sentence; its own text never reaches the page.</summary>
    [Fact]
    public async Task AnUntranslatedFailureNeverShowsItsOwnText()
    {
        using var model = new CloudAgentsViewModel(new ThrowingCloudHost(new InvalidOperationException("token=" + Canary)), _clock);

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Error);
        model.LoadError.ShouldBe(CloudCopy.LoadFailed);
        model.LoadError.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A refresh before any successful load loads instead, so Refresh recovers from the error state too.</summary>
    [Fact]
    public async Task RefreshAfterAFailedLoadLoadsAgain()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle));
        host.FailNext("List", new CloudHostException("Offline."));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.State.ShouldBe(CloudAgentsViewState.Error);

        await model.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Ready);
    }

    /// <summary>Rows show the status in words with a glyph, the repositories on the branch and the relative last activity, newest activity first.</summary>
    [Fact]
    public async Task RowsShowStatusWordsGlyphsAndActivityNewestFirst()
    {
        var older = CloudFixtures.Agent("old", CloudAgentStatus.Stopped, "Old agent") with { LastActivityAt = CloudFixtures.Origin.AddHours(-3) };
        var newer = CloudFixtures.Agent("new", CloudAgentStatus.Running, "New agent") with { LastActivityAt = CloudFixtures.Origin.AddMinutes(-2) };
        var untouched = CloudFixtures.Agent("none", CloudAgentStatus.Provisioning, "No activity") with { LastActivityAt = null, CreatedAt = CloudFixtures.Origin.AddDays(-5) };
        var host = CloudFixtures.Host(_clock, older, untouched, newer);
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.Rows.Select(row => row.Id).ShouldBe(["new", "old", "none"]);
        var row = model.Rows[0];
        row.Name.ShouldBe("New agent");
        row.StatusText.ShouldBe("Running");
        row.IconKey.ShouldBe(StatusIcon.Running);
        row.DetailText.ShouldBe("owner/app on autonogrammer/new");
        row.LastActivityText.ShouldBe(RelativeTimeText.Format(CloudFixtures.Origin.AddMinutes(-2), CloudFixtures.Origin));
        row.AccessibleName.ShouldBe($"New agent, cloud agent, Running, {row.LastActivityText}");
        model.Rows[2].LastActivityText.ShouldBe(CloudCopy.NoActivity);
        model.Rows[2].StatusText.ShouldBe("Provisioning");
    }

    /// <summary>A blank name reads as the untitled name and an agent without repositories says so; control characters never reach a row.</summary>
    [Fact]
    public async Task BlankNamesAndMissingRepositoriesReadAsWords()
    {
        var blank = CloudFixtures.Agent("a", CloudAgentStatus.Idle, "   ") with { Repositories = [], Branch = null };
        var control = CloudFixtures.Agent("b", CloudAgentStatus.Idle, "Two\nlines") with { LastActivityAt = CloudFixtures.Origin.AddMinutes(-1) };
        using var model = await CloudFixtures.LoadedAsync(CloudFixtures.Host(_clock, blank, control), _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var blankRow = model.Rows.Single(row => string.Equals(row.Id, "a", StringComparison.Ordinal));
        blankRow.Name.ShouldBe(CloudCopy.UntitledAgent);
        blankRow.DetailText.ShouldBe(CloudCopy.NoRepositories);
        model.Rows.Single(row => string.Equals(row.Id, "b", StringComparison.Ordinal)).Name.ShouldNotContain("\n", Case.Sensitive);
    }

    /// <summary>Every status has its own row words; the page never shows a number for a status.</summary>
    [Fact]
    public async Task EveryStatusReadsInWordsInTheList()
    {
        var agents = CloudAgentStatusMap.All.Select(status => CloudFixtures.Agent(status.ToString(), status)).ToArray();
        using var model = await CloudFixtures.LoadedAsync(CloudFixtures.Host(_clock, agents), _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.Rows.Select(row => row.StatusText).Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
        model.Rows.ShouldAllBe(row => row.AccessibleName.Contains("cloud agent", StringComparison.Ordinal));
    }

    /// <summary>The status filter keeps one status, says so when nothing matches, and the first entry shows every status again.</summary>
    [Fact]
    public async Task TheStatusFilterKeepsOneStatus()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running), CloudFixtures.Agent("b", CloudAgentStatus.Idle), CloudFixtures.Agent("c", CloudAgentStatus.Running));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.FilterOptions[0].Status.ShouldBeNull();
        model.FilterOptions[0].Label.ShouldBe(CloudCopy.AllStatuses);
        model.FilterOptions.Count.ShouldBe(11);

        model.StatusFilter = CloudAgentStatus.Running;
        model.Rows.Select(row => row.Id).Order(StringComparer.Ordinal).ShouldBe(["a", "c"]);
        model.TotalCount.ShouldBe(3);
        model.HasNoMatches.ShouldBeFalse();

        model.StatusFilter = CloudAgentStatus.Failed;
        model.Rows.ShouldBeEmpty();
        model.HasNoMatches.ShouldBeTrue();
        model.NoMatchText.ShouldBe(CloudCopy.NoMatches("Failed"));

        model.StatusFilter = null;
        model.Rows.Count.ShouldBe(3);
        model.HasNoMatches.ShouldBeFalse();
        model.NoMatchText.ShouldBeEmpty();
    }

    /// <summary>Selecting an agent builds its details; an unknown identifier or null clears the selection.</summary>
    [Fact]
    public async Task SelectionBuildsTheDetailsAndUnknownIdentifiersClearIt()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle), CloudFixtures.Agent("b", CloudAgentStatus.Stopped));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var changed = new List<string>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        model.Select("b");

        model.SelectedId.ShouldBe("b");
        model.Details.ShouldNotBeNull().Id.ShouldBe("b");
        model.Details.CanWake.ShouldBeTrue();
        changed.ShouldContain(nameof(CloudAgentsViewModel.Details), StringComparer.Ordinal);

        var first = model.Details;
        model.Select("b");
        model.Details.ShouldBeSameAs(first);

        model.Select("nobody");
        model.SelectedId.ShouldBeNull();
        model.Details.ShouldBeNull();

        model.Select("a");
        model.Select(null);
        model.Details.ShouldBeNull();
    }

    /// <summary>When the host reports a change the list refreshes by itself: a new agent appears and the selected agent's details follow its status.</summary>
    [Fact]
    public async Task AHostChangeRefreshesTheListAndTheSelectedDetails()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.Select("a");

        host.Add(CloudFixtures.Agent("b", CloudAgentStatus.Provisioning));
        host.SetStatus("a", CloudAgentStatus.Stopped);
        host.PublishChanges();

        model.Rows.Count.ShouldBe(2);
        model.Details.ShouldNotBeNull().Status.ShouldBe(CloudAgentStatus.Stopped);
        model.Details.CanWake.ShouldBeTrue();
        model.Rows.Single(row => string.Equals(row.Id, "a", StringComparison.Ordinal)).StatusText.ShouldBe("Stopped");
    }

    /// <summary>The refresh goes through the head's dispatch hook, so a change that arrives on another thread never touches the model there.</summary>
    [Fact]
    public async Task TheRefreshRunsThroughTheDispatchHook()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        var queued = new List<Action>();
        using var model = new CloudAgentsViewModel(host, _clock, new CloudAgentsOptions { Dispatch = queued.Add });
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Add(CloudFixtures.Agent("b", CloudAgentStatus.Idle));
        host.PublishChanges();

        model.Rows.Count.ShouldBe(1, "nothing ran yet: the change waits for the UI thread");
        queued.Count.ShouldBe(1);
        queued[0]();
        model.Rows.Count.ShouldBe(2);
    }

    /// <summary>Changes that arrive while a refresh is running are folded into one more pass, not one pass each.</summary>
    [Fact]
    public async Task ChangesDuringARefreshAreFoldedIntoOneMorePass()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.CountOf("List").ShouldBe(1);
        var release = host.HoldNext("List");

        var pending = model.RefreshAsync(TestContext.Current.CancellationToken);
        model.IsRefreshing.ShouldBeTrue();
        host.PublishChanges();
        host.PublishChanges();
        host.PublishChanges();
        release();
        await pending.ConfigureAwait(true);

        host.CountOf("List").ShouldBe(3, "the held pass plus exactly one more");
        model.IsRefreshing.ShouldBeFalse();
    }

    /// <summary>A failed refresh keeps the last good list, says why, and the next good refresh clears the message.</summary>
    [Fact]
    public async Task AFailedRefreshKeepsTheLastGoodListAndSaysWhy()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.FailNext("List", new CloudHostException("The service did not answer."));

        await model.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(CloudAgentsViewState.Ready);
        model.Rows.Count.ShouldBe(1);
        model.RefreshError.ShouldBe("The service did not answer.");

        await model.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.RefreshError.ShouldBeEmpty();
    }

    /// <summary>When the selected agent disappears from the host the selection and the details go with it.</summary>
    [Fact]
    public async Task ASelectedAgentThatDisappearsClearsTheSelection()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running), CloudFixtures.Agent("b", CloudAgentStatus.Idle));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.Select("a");

        await host.DeleteAsync("a", TestContext.Current.CancellationToken).ConfigureAwait(true);
        await model.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.SelectedId.ShouldBeNull();
        model.Details.ShouldBeNull();
        model.Rows.Select(row => row.Id).ShouldBe(["b"]);
    }

    /// <summary>After Dispose the model no longer listens to the host.</summary>
    [Fact]
    public async Task DisposeStopsListeningToTheHost()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Running));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.Dispose();

        host.Add(CloudFixtures.Agent("b", CloudAgentStatus.Idle));
        host.PublishChanges();
        await model.RefreshAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.CountOf("List").ShouldBe(1);
        model.Rows.Count.ShouldBe(1);
    }

    /// <summary>The model that begins a new agent selects the new agent once it is created, even before the host lists it.</summary>
    [Fact]
    public async Task ACreatedAgentIsSelectedAndListed()
    {
        var host = CloudFixtures.Host(_clock);
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var form = model.BeginNewAgent().ShouldNotBeNull();
        form.Name = "Fix the build";
        form.RepositoryText = "https://github.com/owner/app";
        _ = form.AddRepository();
        form.CostConfirmed = true;

        var result = await form.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.Created);
        model.State.ShouldBe(CloudAgentsViewState.Ready);
        model.SelectedId.ShouldBe(result.Agent.ShouldNotBeNull().Id);
        model.Rows.Select(row => row.Id).ShouldBe([result.Agent.Id]);
        model.Rows[0].StatusText.ShouldBe("Provisioning");
    }

    /// <summary>A reached agent cap disables New cloud agent with the reason and begins no dialog.</summary>
    [Fact]
    public async Task AReachedAgentCapDisablesNewCloudAgentWithAReason()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle));
        host.MaxAgents = 1;
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.CanCreate.ShouldBeFalse();
        model.CreateDisabledReason.ShouldBe(CloudCopy.AgentCapReached(1));
        model.BeginNewAgent().ShouldBeNull();
    }

    /// <summary>Deleting the selected agent from its details removes it from the list and clears the selection; the last one leaves the empty state.</summary>
    [Fact]
    public async Task DeletingTheSelectedAgentLeavesTheList()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Stopped));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.Select("a");
        var details = model.Details.ShouldNotBeNull();

        details.RequestDelete();
        await details.ConfirmDeleteAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Agents.ShouldBeEmpty();
        model.Rows.ShouldBeEmpty();
        model.SelectedId.ShouldBeNull();
        model.State.ShouldBe(CloudAgentsViewState.Empty);
    }

    /// <summary>Open in Agents from the details reaches the owner of the page with the session to open.</summary>
    [Fact]
    public async Task OpenInAgentsReachesTheOwnerWithTheSession()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle, sessionId: "session-42"));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        CloudAgentOpenEventArgs? opened = null;
        model.OpenInAgentsRequested += (_, args) => opened = args;
        model.Select("a");

        model.Details.ShouldNotBeNull().OpenInAgents();

        opened.ShouldNotBeNull().AgentId.ShouldBe("a");
        opened.SessionId.ShouldBe("session-42");
    }

    /// <summary>The relative times follow the clock when the view ticks.</summary>
    [Fact]
    public async Task RelativeTimesFollowTheClock()
    {
        var host = CloudFixtures.Host(_clock, CloudFixtures.Agent("a", CloudAgentStatus.Idle));
        using var model = await CloudFixtures.LoadedAsync(host, _clock, TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.Rows[0].LastActivityText.ShouldBe(RelativeTimeText.Format(CloudFixtures.Origin, CloudFixtures.Origin));

        _clock.Advance(TimeSpan.FromMinutes(7));
        model.RefreshTimes();

        model.Rows[0].LastActivityText.ShouldBe(RelativeTimeText.Format(CloudFixtures.Origin, CloudFixtures.Origin.AddMinutes(7)));
        model.Rows[0].LastActivityText.ShouldNotBe(RelativeTimeText.Format(CloudFixtures.Origin, CloudFixtures.Origin), StringComparer.Ordinal);
    }
}
