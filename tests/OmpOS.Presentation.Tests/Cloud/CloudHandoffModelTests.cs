using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>The hand-off dialog model: nothing is called before the cost is confirmed, a bad repository is refused, and each ending of the hand-off reads in plain words.</summary>
public sealed class CloudHandoffModelTests
{
    private const string Canary = "s3cr3t-canary-value";
    private const string TaskText = "Fix the flaky test\nIt fails on CI only.";
    private const string Repository = "https://github.com/owner/app";

    private readonly ManualTimeProvider _clock = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CloudHandoffModel Model(FakeCloudAgentsHost host, string task = TaskText, string? repository = Repository, CloudSetup? setup = null) =>
        new(new CloudHandoff(host), setup ?? new CloudSetup { IsConfigured = true }, task, repository);

    /// <summary>The cost notice names the idle time of the setup in the words of the cost sentence.</summary>
    [Fact]
    public void TheCostNoticeNamesTheIdleTimeOfTheSetup()
    {
        var model = Model(CloudFixtures.Host(_clock), setup: new CloudSetup { IsConfigured = true, DefaultIdleMinutes = 20 });

        model.CostNotice.ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 20 minutes.");
        Model(CloudFixtures.Host(_clock), setup: new CloudSetup { IsConfigured = true, DefaultIdleMinutes = 1 }).CostNotice
            .ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 1 minute.");
    }

    /// <summary>The task is kept as typed and the suggested repository is the starting text.</summary>
    [Fact]
    public void TheTaskIsKeptAndTheSuggestedRepositoryIsTheStartingText()
    {
        var model = Model(CloudFixtures.Host(_clock));

        model.Task.ShouldBe(TaskText);
        model.RepositoryText.ShouldBe(Repository);
        model.CostConfirmed.ShouldBeFalse();
        model.Status.ShouldBeEmpty();
        model.HasError.ShouldBeFalse();
        model.Result.ShouldBeNull();
    }

    /// <summary>Hand off is available only with a task, a valid repository and the confirmed cost, and each missing piece has its own reason.</summary>
    [Fact]
    public void CanStartNeedsATaskAValidRepositoryAndTheCostConfirmation()
    {
        var host = CloudFixtures.Host(_clock);

        var noTask = Model(host, task: "   ");
        noTask.CostConfirmed = true;
        noTask.CanStart.ShouldBeFalse();
        noTask.WhyDisabled.ShouldBe(CloudCopy.HandoffNeedsPrompt);

        var noRepository = Model(host, repository: null);
        noRepository.CostConfirmed = true;
        noRepository.CanStart.ShouldBeFalse();
        noRepository.WhyDisabled.ShouldBe(CloudCopy.RepositoryRequired);
        noRepository.RepositoryError.ShouldBeEmpty("an untouched entry shows no error");

        var badRepository = Model(host, repository: "http://github.com/owner/app");
        badRepository.CostConfirmed = true;
        badRepository.CanStart.ShouldBeFalse();
        badRepository.RepositoryError.ShouldBe(CloudCopy.RepositoryNotHttps);
        badRepository.WhyDisabled.ShouldBe(CloudCopy.RepositoryNotHttps);

        var unconfirmed = Model(host);
        unconfirmed.CanStart.ShouldBeFalse();
        unconfirmed.WhyDisabled.ShouldBe(CloudCopy.CostNotConfirmed);

        unconfirmed.CostConfirmed = true;
        unconfirmed.CanStart.ShouldBeTrue();
        unconfirmed.WhyDisabled.ShouldBeEmpty();
    }

    /// <summary>A hand-off before the cost is confirmed makes no host call at all, not even a read.</summary>
    [Fact]
    public async Task StartingBeforeTheCostIsConfirmedMakesNoHostCall()
    {
        var host = CloudFixtures.Host(_clock);
        var model = Model(host);

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsCostConfirmation);
        result.Message.ShouldBe(CloudCopy.CostNotConfirmed);
        host.Calls.ShouldBeEmpty();
        model.Result.ShouldBe(result);
        model.Status.ShouldBe(CloudCopy.CostNotConfirmed);
        model.HasError.ShouldBeTrue();
    }

    /// <summary>An invalid repository is refused with no host call, and neither the result nor the error repeats a credential that was typed.</summary>
    [Fact]
    public async Task AnInvalidRepositoryIsRefusedWithNoCallAndNeverRepeatsTheEntry()
    {
        var host = CloudFixtures.Host(_clock);
        var model = Model(host, repository: "https://user:" + Canary + "@github.com/owner/app");
        model.CostConfirmed = true;

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsInput);
        result.Message.ShouldBe(CloudCopy.RepositoryHasCredentials);
        result.Message.ShouldNotContain(Canary, Case.Sensitive);
        model.RepositoryError.ShouldBe(CloudCopy.RepositoryHasCredentials);
        model.RepositoryError.ShouldNotContain(Canary, Case.Sensitive);
        host.Calls.ShouldBeEmpty();
    }

    /// <summary>A missing task needs input and makes no call.</summary>
    [Fact]
    public async Task ANeedsInputAttemptWithoutATaskMakesNoCall()
    {
        var host = CloudFixtures.Host(_clock);
        var model = Model(host, task: " ");
        model.CostConfirmed = true;

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsInput);
        result.Message.ShouldBe(CloudCopy.HandoffNeedsPrompt);
        host.Calls.ShouldBeEmpty();
    }

    /// <summary>A confirmed hand-off creates the agent with the task as its first prompt, the repository and the idle time of the setup, and raises Started once.</summary>
    [Fact]
    public async Task AConfirmedHandoffStartsTheAgentAndRaisesStarted()
    {
        var host = CloudFixtures.Host(_clock);
        var model = Model(host, setup: new CloudSetup { IsConfigured = true, DefaultIdleMinutes = 30 });
        var started = new List<CloudAgentRecord>();
        model.Started += (_, e) => started.Add(e.Agent);
        model.CostConfirmed = true;

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Started);
        result.Message.ShouldBe(CloudCopy.HandoffStarted("Fix the flaky test"));
        var request = host.CreateRequests.ShouldHaveSingleItem();
        request.Prompt.ShouldBe(TaskText);
        request.IdleMinutes.ShouldBe(30);
        request.Repositories.Select(repository => repository.Address).ShouldBe([Repository]);
        request.SecretNames.ShouldBeEmpty();
        started.ShouldHaveSingleItem().ShouldBe(result.Agent);
        model.Result.ShouldBe(result);
        model.Status.ShouldBe(result.Message);
        model.HasError.ShouldBeFalse();
        model.IsBusy.ShouldBeFalse();
        model.CanStart.ShouldBeFalse("the agent exists; a second start would create another");

        (await model.StartAsync(Token).ConfigureAwait(true)).ShouldBe(result);
        host.CountOf("Create").ShouldBe(1);
        started.Count.ShouldBe(1);
    }

    /// <summary>While the host works the model is busy: Start and Cancel do nothing and a second attempt makes no second call.</summary>
    [Fact]
    public async Task WhileTheHostWorksTheModelIsBusy()
    {
        var host = CloudFixtures.Host(_clock);
        var release = host.HoldNext("Create");
        var model = Model(host);
        var cancelled = 0;
        model.CancelRequested += (_, _) => cancelled++;
        model.CostConfirmed = true;

        var pending = model.StartAsync(Token);

        model.IsBusy.ShouldBeTrue();
        model.CanStart.ShouldBeFalse();
        model.Status.ShouldBe(CloudCopy.HandoffInProgress);
        model.HasError.ShouldBeFalse();
        model.Cancel();
        cancelled.ShouldBe(0);
        (await model.StartAsync(Token).ConfigureAwait(true)).Message.ShouldBe(CloudCopy.CreateInProgress);

        release();
        var result = await pending.ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Started);
        model.IsBusy.ShouldBeFalse();
        host.CountOf("Create").ShouldBe(1);
    }

    /// <summary>Each refusal of the service shows the service's words as an error, keeps the dialog open and lets the user try again.</summary>
    [Theory]
    [InlineData(CloudFailureKind.LimitReached, CloudHandoffOutcome.LimitReached)]
    [InlineData(CloudFailureKind.QuotaExceeded, CloudHandoffOutcome.QuotaExceeded)]
    [InlineData(CloudFailureKind.Rejected, CloudHandoffOutcome.Rejected)]
    [InlineData(CloudFailureKind.Unavailable, CloudHandoffOutcome.Failed)]
    [InlineData(CloudFailureKind.Unauthorized, CloudHandoffOutcome.NotAvailable)]
    public async Task AServiceRefusalIsShownAndTheDialogStaysOpen(CloudFailureKind kind, CloudHandoffOutcome outcome)
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("Create", new CloudHostException(kind, "The service said no."));
        var model = Model(host);
        var started = 0;
        model.Started += (_, _) => started++;
        model.CostConfirmed = true;

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(outcome);
        model.Status.ShouldBe("The service said no.");
        model.HasError.ShouldBeTrue();
        model.IsBusy.ShouldBeFalse();
        model.CanStart.ShouldBeTrue("the user may try again");
        started.ShouldBe(0);

        var retry = await model.StartAsync(Token).ConfigureAwait(true);

        retry.Outcome.ShouldBe(CloudHandoffOutcome.Started);
        started.ShouldBe(1);
    }

    /// <summary>A failure the host did not translate reads as the plain failure sentence and never shows its own text.</summary>
    [Fact]
    public async Task AnUntranslatedFailureReadsAsAPlainFailure()
    {
        var model = new CloudHandoffModel(
            new CloudHandoff(new ThrowingCloudHost(new InvalidOperationException("token=" + Canary))),
            new CloudSetup { IsConfigured = true },
            TaskText,
            Repository)
        {
            CostConfirmed = true,
        };

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Failed);
        model.Status.ShouldBe(CloudCopy.CreateFailed);
        model.Status.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>Changing the repository never clears the cost confirmation, and an edit after a refusal removes the stale message.</summary>
    [Fact]
    public async Task EditingTheRepositoryKeepsTheConfirmationAndClearsAStaleRefusal()
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("Create", new CloudHostException(CloudFailureKind.Rejected, "The service said no."));
        var model = Model(host);
        model.CostConfirmed = true;
        _ = await model.StartAsync(Token).ConfigureAwait(true);
        model.HasError.ShouldBeTrue();

        model.RepositoryText = "https://github.com/owner/other";

        model.CostConfirmed.ShouldBeTrue();
        model.Status.ShouldBeEmpty();
        model.HasError.ShouldBeFalse();
        model.Result.ShouldBeNull();
        model.CanStart.ShouldBeTrue();

        model.RepositoryText = "not a url";
        model.CostConfirmed.ShouldBeTrue();
        model.CanStart.ShouldBeFalse();
        model.RepositoryError.ShouldNotBeEmpty();

        model.RepositoryText = string.Empty;
        model.RepositoryError.ShouldBeEmpty();
        model.CostConfirmed.ShouldBeTrue();
    }

    /// <summary>Cancel asks the owner to close the dialog and makes no host call.</summary>
    [Fact]
    public void CancelAsksTheOwnerToCloseAndCallsNothing()
    {
        var host = CloudFixtures.Host(_clock);
        var model = Model(host);
        var cancelled = 0;
        model.CancelRequested += (_, _) => cancelled++;

        model.Cancel();

        cancelled.ShouldBe(1);
        host.Calls.ShouldBeEmpty();
    }

    /// <summary>A host that is not set up is reported as unavailable, shown as an error, with no agent created.</summary>
    [Fact]
    public async Task ANotConfiguredHostIsShownAsAnError()
    {
        var host = CloudFixtures.Host(_clock);
        host.Setup = CloudSetup.NotConfigured("Sign in to the OmpOS service first.");
        var model = Model(host);
        model.CostConfirmed = true;

        var result = await model.StartAsync(Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NotAvailable);
        model.Status.ShouldBe("Sign in to the OmpOS service first.");
        model.HasError.ShouldBeTrue();
        host.CountOf("Create").ShouldBe(0);
    }
}
