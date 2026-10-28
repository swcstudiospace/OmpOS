using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>The details of one cloud agent: the facts, the status history, and the commands Wake, Stop, Delete with confirmation, Send, pull requests and Open in Agents.</summary>
public sealed class CloudAgentDetailsModelTests
{
    private const string Canary = "s3cr3t-canary-value";

    private readonly ManualTimeProvider _clock = new();

    private (FakeCloudAgentsHost Host, CloudAgentDetailsModel Details) Open(CloudAgentStatus status)
    {
        var agent = CloudFixtures.Agent("a", status, "Fix the build") with { BaseBranch = "main", EnvironmentName = "Production", Model = "fast" };
        var host = CloudFixtures.Host(_clock, agent);
        return (host, new CloudAgentDetailsModel(host, _clock, agent));
    }

    /// <summary>The details give each fact a labelled line: status words and explanation, branches, environment, model, idle time and last activity.</summary>
    [Fact]
    public void TheDetailsStateTheFactsInWords()
    {
        var (_, details) = Open(CloudAgentStatus.Idle);

        details.Name.ShouldBe("Fix the build");
        details.StatusText.ShouldBe("Idle");
        details.StatusDescription.ShouldBe(CloudCopy.DescribeIdle);
        details.IconKey.ShouldBe(StatusIcon.Ring);
        details.Repositories.ShouldBe(["owner/app"]);
        details.BranchText.ShouldBe("Branch: autonogrammer/a");
        details.BaseBranchText.ShouldBe("Starts from main");
        details.EnvironmentText.ShouldBe("Environment: Production");
        details.ModelText.ShouldBe("Model: fast");
        details.IdleText.ShouldBe("Sleeps after 15 minutes without activity");
        details.LastActivityText.ShouldBe($"Last activity: {RelativeTimeText.Format(CloudFixtures.Origin, CloudFixtures.Origin)}");
    }

    /// <summary>Facts the host did not report read as words, not as blanks.</summary>
    [Fact]
    public void MissingFactsReadAsWords()
    {
        var agent = CloudFixtures.Agent("a", CloudAgentStatus.Provisioning) with { Branch = null, LastActivityAt = null, IdleMinutes = 0, Timeline = [] };
        var details = new CloudAgentDetailsModel(CloudFixtures.Host(_clock, agent), _clock, agent);

        details.BranchText.ShouldBe("Branch: not created yet");
        details.BaseBranchText.ShouldBe("Starts from the default branch");
        details.EnvironmentText.ShouldBe("Environment: default");
        details.ModelText.ShouldBe("Model: service default");
        details.IdleText.ShouldBeEmpty();
        details.LastActivityText.ShouldBe($"Last activity: {CloudCopy.NoActivity}");
        details.Timeline.ShouldBeEmpty();
    }

    /// <summary>The status history lists each step in order with its words, glyph, relative time and the host's note.</summary>
    [Fact]
    public void TheStatusHistoryListsEveryStepInOrder()
    {
        var agent = CloudFixtures.Agent("a", CloudAgentStatus.Failed) with
        {
            Timeline =
            [
                new CloudStatusEvent(CloudFixtures.Origin.AddMinutes(-10), CloudAgentStatus.Provisioning),
                new CloudStatusEvent(CloudFixtures.Origin.AddMinutes(-4), CloudAgentStatus.Running),
                new CloudStatusEvent(CloudFixtures.Origin.AddMinutes(-1), CloudAgentStatus.Failed, "The worker ran out of memory."),
            ],
            StatusDetail = "The worker ran out of memory.",
        };
        var details = new CloudAgentDetailsModel(CloudFixtures.Host(_clock, agent), _clock, agent);

        details.Timeline.Select(row => row.StatusText).ShouldBe(["Provisioning", "Running", "Failed"]);
        details.Timeline[2].IconKey.ShouldBe(StatusIcon.Cross);
        details.Timeline[2].Note.ShouldBe("The worker ran out of memory.");
        details.Timeline[2].AccessibleName.ShouldBe($"Failed, {details.Timeline[2].TimeText}, The worker ran out of memory.");
        details.Timeline[0].AccessibleName.ShouldBe($"Provisioning, {details.Timeline[0].TimeText}");
        details.StatusDetail.ShouldBe("The worker ran out of memory.");
    }

    /// <summary>Waking a stopped agent calls the host once, shows the new status and announces it; the owner is told to refresh.</summary>
    [Fact]
    public async Task WakeWakesAStoppedAgent()
    {
        var (host, details) = Open(CloudAgentStatus.Stopped);
        var completed = 0;
        details.ActionCompleted += (_, _) => completed++;
        details.CanWake.ShouldBeTrue();
        details.CanStop.ShouldBeFalse();

        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Calls.ShouldBe(["Wake:a"]);
        details.Status.ShouldBe(CloudAgentStatus.Starting);
        details.Notice.ShouldBe(CloudCopy.WakeRequested("Fix the build"));
        details.ActionError.ShouldBeEmpty();
        details.CanWake.ShouldBeFalse();
        details.CanStop.ShouldBeTrue();
        completed.ShouldBe(1);
    }

    /// <summary>Stopping a running agent calls the host and shows the stopped status.</summary>
    [Fact]
    public async Task StopStopsARunningAgent()
    {
        var (host, details) = Open(CloudAgentStatus.Running);

        await details.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Calls.ShouldBe(["Stop:a"]);
        details.Status.ShouldBe(CloudAgentStatus.Stopped);
        details.Notice.ShouldBe(CloudCopy.StopRequested("Fix the build"));
    }

    /// <summary>A command that does not fit the status does nothing and makes no call.</summary>
    [Fact]
    public async Task ACommandThatDoesNotFitTheStatusMakesNoCall()
    {
        var (runningHost, running) = Open(CloudAgentStatus.Running);
        await running.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var (stoppedHost, stopped) = Open(CloudAgentStatus.Stopped);
        await stopped.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await stopped.SendPromptAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        runningHost.Calls.ShouldBeEmpty();
        stoppedHost.Calls.ShouldBeEmpty();
        stopped.CanPrompt.ShouldBeFalse();
    }

    /// <summary>While a command runs every command is disabled and a second Wake makes no second call.</summary>
    [Fact]
    public async Task WhileACommandRunsEveryCommandIsDisabled()
    {
        var (host, details) = Open(CloudAgentStatus.Stopped);
        var release = host.HoldNext("Wake");

        var pending = details.WakeAsync(TestContext.Current.CancellationToken);

        details.IsBusy.ShouldBeTrue();
        details.CanWake.ShouldBeFalse();
        details.CanDelete.ShouldBeFalse();
        details.PullRequests.ShouldAllBe(row => !row.CanAct);
        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        release();
        await pending.ConfigureAwait(true);

        host.CountOf("Wake").ShouldBe(1);
        details.IsBusy.ShouldBeFalse();
    }

    /// <summary>A failed command shows the host's words, keeps the status, and the next try can succeed.</summary>
    [Fact]
    public async Task AFailedCommandShowsItsReasonAndCanBeRepeated()
    {
        var (host, details) = Open(CloudAgentStatus.Stopped);
        host.FailNext("Wake", new CloudHostException(CloudFailureKind.LimitReached, "Too many workers are awake."));
        var completed = 0;
        details.ActionCompleted += (_, _) => completed++;

        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        details.ActionError.ShouldBe("Too many workers are awake.");
        details.Notice.ShouldBeEmpty();
        details.Status.ShouldBe(CloudAgentStatus.Stopped);
        details.IsBusy.ShouldBeFalse();
        completed.ShouldBe(0);

        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        details.ActionError.ShouldBeEmpty();
        details.Status.ShouldBe(CloudAgentStatus.Starting);
    }

    /// <summary>An exception the host did not translate reads as the plain sentence; its own text never shows.</summary>
    [Fact]
    public async Task AnUntranslatedFailureNeverShowsItsOwnText()
    {
        var agent = CloudFixtures.Agent("a", CloudAgentStatus.Stopped);
        var details = new CloudAgentDetailsModel(new ThrowingCloudHost(new InvalidOperationException("token=" + Canary)), _clock, agent);

        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        details.ActionError.ShouldBe(CloudCopy.ActionFailed);
        details.ActionError.ShouldNotContain(Canary, Case.Sensitive);
        details.IsBusy.ShouldBeFalse();
    }

    /// <summary>Delete asks first: the confirmation names what is removed, Cancel keeps the agent, and nothing is deleted before the confirmation.</summary>
    [Fact]
    public async Task DeleteNeedsAConfirmationAndCancelKeepsTheAgent()
    {
        var (host, details) = Open(CloudAgentStatus.Idle);

        details.RequestDelete();

        details.IsConfirmingDelete.ShouldBeTrue();
        details.DeleteWarning.ShouldBe(CloudCopy.DeleteWarning("Fix the build"));
        details.DeleteWarning.ShouldContain("pull requests stay on GitHub", Case.Sensitive);
        details.CanDelete.ShouldBeFalse("the confirmation is open");
        host.CountOf("Delete").ShouldBe(0);

        details.CancelDelete();

        details.IsConfirmingDelete.ShouldBeFalse();
        details.CanDelete.ShouldBeTrue();
        await details.ConfirmDeleteAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.CountOf("Delete").ShouldBe(0, "a confirm without an open confirmation does nothing");
        host.Agents.Count.ShouldBe(1);
    }

    /// <summary>Confirming deletes through the host once, announces it, disables every command and tells the owner.</summary>
    [Fact]
    public async Task ConfirmingDeletesTheAgent()
    {
        var (host, details) = Open(CloudAgentStatus.Idle);
        var completed = 0;
        details.ActionCompleted += (_, _) => completed++;

        details.RequestDelete();
        await details.ConfirmDeleteAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Calls.ShouldBe(["Delete:a"]);
        host.Agents.ShouldBeEmpty();
        details.IsDeleted.ShouldBeTrue();
        details.IsConfirmingDelete.ShouldBeFalse();
        details.Notice.ShouldBe(CloudCopy.DeleteRequested("Fix the build"));
        details.CanStop.ShouldBeFalse();
        details.CanDelete.ShouldBeFalse();
        details.CanPrompt.ShouldBeFalse();
        details.CanOpenInAgents.ShouldBeFalse();
        completed.ShouldBe(1);
    }

    /// <summary>A failed delete keeps the agent, hides the confirmation and shows the reason.</summary>
    [Fact]
    public async Task AFailedDeleteKeepsTheAgent()
    {
        var (host, details) = Open(CloudAgentStatus.Idle);
        host.FailNext("Delete", new CloudHostException(CloudFailureKind.Conflict, "The agent is busy."));

        details.RequestDelete();
        await details.ConfirmDeleteAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        details.IsDeleted.ShouldBeFalse();
        details.ActionError.ShouldBe("The agent is busy.");
        host.Agents.Count.ShouldBe(1);
        details.CanDelete.ShouldBeTrue();
    }

    /// <summary>A deleting agent cannot be deleted again.</summary>
    [Fact]
    public void ADeletingAgentCannotBeDeletedAgain()
    {
        var (_, details) = Open(CloudAgentStatus.Deleting);

        details.RequestDelete();

        details.IsConfirmingDelete.ShouldBeFalse();
        details.CanDelete.ShouldBeFalse();
    }

    /// <summary>A status update that makes Delete impossible closes an open confirmation.</summary>
    [Fact]
    public void AnUpdateThatForbidsDeleteClosesTheConfirmation()
    {
        var (_, details) = Open(CloudAgentStatus.Idle);
        details.RequestDelete();

        details.Update(details.Record with { Status = CloudAgentStatus.Deleting });

        details.IsConfirmingDelete.ShouldBeFalse();
    }

    /// <summary>An update keeps the open confirmation and the typed message; an update for another agent is refused.</summary>
    [Fact]
    public void AnUpdateKeepsTheUsersWorkAndRefusesAForeignRecord()
    {
        var (_, details) = Open(CloudAgentStatus.Running);
        details.PromptText = "Keep going";
        details.RequestDelete();

        details.Update(details.Record with { Status = CloudAgentStatus.Idle });

        details.Status.ShouldBe(CloudAgentStatus.Idle);
        details.IsConfirmingDelete.ShouldBeTrue();
        details.PromptText.ShouldBe("Keep going");
        Should.Throw<ArgumentException>(() => details.Update(CloudFixtures.Agent("other", CloudAgentStatus.Idle)));
    }

    /// <summary>Send needs text and a live agent; the text goes to the host trimmed and is cleared once accepted.</summary>
    [Fact]
    public async Task SendDeliversTheTrimmedMessageAndClearsTheBox()
    {
        var (host, details) = Open(CloudAgentStatus.Running);
        details.CanSendPrompt.ShouldBeFalse("nothing typed");

        details.PromptText = "   ";
        details.CanSendPrompt.ShouldBeFalse();
        details.PromptText = "  Run the tests.  ";
        details.CanSendPrompt.ShouldBeTrue();
        await details.SendPromptAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Prompts.ShouldBe(["a: Run the tests."]);
        details.PromptText.ShouldBeEmpty();
        details.Notice.ShouldBe(CloudCopy.PromptSent("Fix the build"));
    }

    /// <summary>A rejected message stays in the box so the user can send it again.</summary>
    [Fact]
    public async Task ARejectedMessageStaysInTheBox()
    {
        var (host, details) = Open(CloudAgentStatus.Running);
        host.FailNext("Prompt", new CloudHostException("The worker did not accept it."));
        details.PromptText = "Run the tests.";

        await details.SendPromptAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        details.PromptText.ShouldBe("Run the tests.");
        details.ActionError.ShouldBe("The worker did not accept it.");
        details.CanSendPrompt.ShouldBeTrue();
    }

    /// <summary>A repository without a pull request offers to create a draft one; the host gets the agent's name as title and the base branch, and the page opens it.</summary>
    [Fact]
    public async Task CreatePullRequestOpensADraftAndShowsIt()
    {
        var (host, details) = Open(CloudAgentStatus.Idle);
        var row = details.PullRequests.ShouldHaveSingleItem();
        row.HasPullRequest.ShouldBeFalse();
        row.ActionLabel.ShouldBe(CloudCopy.CreatePullRequestLabel);
        row.ActionName.ShouldBe(CloudCopy.CreatePullRequestName("owner/app"));
        row.CanAct.ShouldBeTrue();

        await details.OpenPullRequestAsync("https://github.com/owner/app", TestContext.Current.CancellationToken).ConfigureAwait(true);

        var request = host.PullRequestRequests.ShouldHaveSingleItem();
        request.Repository.ShouldBe("https://github.com/owner/app");
        request.Title.ShouldBe("Fix the build");
        request.BaseBranch.ShouldBe("main");
        request.Draft.ShouldBeTrue();
        host.OpenedPullRequests.ShouldHaveSingleItem().Url.ShouldBe(new Uri("https://github.com/owner/app/pull/1"));
        details.Notice.ShouldBe(CloudCopy.PullRequestCreated("owner/app"));
        var now = details.PullRequests.ShouldHaveSingleItem();
        now.HasPullRequest.ShouldBeTrue();
        now.ActionLabel.ShouldBe(CloudCopy.OpenPullRequestLabel);
    }

    /// <summary>A repository that has a pull request opens it without creating another.</summary>
    [Fact]
    public async Task AnExistingPullRequestIsOpenedNotCreatedAgain()
    {
        var link = new CloudPullRequestLink("https://github.com/owner/app", new Uri("https://github.com/owner/app/pull/7"), "Fix the build");
        var agent = CloudFixtures.Agent("a", CloudAgentStatus.Stopped) with { PullRequests = [link] };
        var host = CloudFixtures.Host(_clock, agent);
        var details = new CloudAgentDetailsModel(host, _clock, agent);
        details.PullRequests[0].HasPullRequest.ShouldBeTrue();
        details.PullRequests[0].Title.ShouldBe("Fix the build");
        details.PullRequests[0].CanAct.ShouldBeTrue("a stopped agent's existing pull request can still be opened");

        await details.OpenPullRequestAsync(link.Repository, TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.PullRequestRequests.ShouldBeEmpty();
        host.OpenedPullRequests.ShouldBe([link]);
        details.Notice.ShouldBe(CloudCopy.PullRequestOpened("owner/app"));
    }

    /// <summary>A pull request cannot be created while the agent provisions, and an address that is not the agent's repository does nothing.</summary>
    [Fact]
    public async Task APullRequestNeedsAnAgentThatHoldsTheBranch()
    {
        var (host, provisioning) = Open(CloudAgentStatus.Provisioning);
        provisioning.PullRequests.ShouldHaveSingleItem().CanAct.ShouldBeFalse();
        await provisioning.OpenPullRequestAsync("https://github.com/owner/app", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var (_, idle) = Open(CloudAgentStatus.Idle);
        await idle.OpenPullRequestAsync("https://github.com/other/repo", TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.Calls.ShouldBeEmpty();
        idle.Notice.ShouldBeEmpty();
    }

    /// <summary>One pull request line per repository, each named by its repository.</summary>
    [Fact]
    public void EveryRepositoryHasItsOwnPullRequestLine()
    {
        var agent = CloudFixtures.Agent("a", CloudAgentStatus.Idle) with { Repositories = [CloudFixtures.Repo("owner/app"), CloudFixtures.Repo("owner/lib")] };
        var details = new CloudAgentDetailsModel(CloudFixtures.Host(_clock, agent), _clock, agent);

        details.PullRequests.Select(row => row.RepositoryName).ShouldBe(["owner/app", "owner/lib"]);
        details.PullRequests.Select(row => row.ActionName).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
    }

    /// <summary>Open in Agents needs a session and a status that has one; it raises the event with that session.</summary>
    [Fact]
    public void OpenInAgentsNeedsASessionAndRaisesTheEvent()
    {
        var (_, details) = Open(CloudAgentStatus.Idle);
        CloudAgentOpenEventArgs? args = null;
        details.OpenInAgentsRequested += (_, e) => args = e;
        details.CanOpenInAgents.ShouldBeTrue();

        details.OpenInAgents();

        args.ShouldNotBeNull().SessionId.ShouldBe("session-a");
        args.AgentId.ShouldBe("a");

        var withoutSession = details.Record with { SessionId = null };
        details.Update(withoutSession);
        details.CanOpenInAgents.ShouldBeFalse();
        args = null;
        details.OpenInAgents();
        args.ShouldBeNull();

        details.Update(details.Record with { SessionId = "session-a", Status = CloudAgentStatus.Provisioning });
        details.CanOpenInAgents.ShouldBeFalse();
    }

    /// <summary>The details announce changes with an empty property name, which tells a binding to read every property again.</summary>
    [Fact]
    public async Task TheDetailsAnnounceEveryPropertyWhenACommandRuns()
    {
        var (_, details) = Open(CloudAgentStatus.Stopped);
        var names = new List<string>();
        details.PropertyChanged += (_, args) => names.Add(args.PropertyName ?? "(null)");

        await details.WakeAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        names.ShouldContain(string.Empty, StringComparer.Ordinal);
    }
}
