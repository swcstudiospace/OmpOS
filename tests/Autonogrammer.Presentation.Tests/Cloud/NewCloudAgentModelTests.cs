using Autonogrammer.Presentation.Cloud;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Cloud;

/// <summary>
/// The new cloud agent dialog: validation, the cost confirmation that must come before any billable create, what is sent (names, never
/// secret values), and what the user sees when a cap, the quota, the service or the network refuses.
/// </summary>
public sealed class NewCloudAgentModelTests
{
    private const string Canary = "s3cr3t-canary-value";

    private readonly ManualTimeProvider _clock = new();

    /// <summary>Create stays disabled while any entry is missing, says which one first, and is enabled only after the cost box is ticked.</summary>
    [Fact]
    public void CreateStaysDisabledUntilEveryEntryIsValidAndTheCostIsConfirmed()
    {
        var host = CloudFixtures.Host(_clock);
        var model = new NewCloudAgentModel(host, new CloudSetup { IsConfigured = true });

        model.CanCreate.ShouldBeFalse();
        model.WhyDisabled.ShouldBe(CloudCopy.NameRequired);

        model.Name = "Fix the build";
        model.WhyDisabled.ShouldBe(CloudCopy.RepositoryNeeded);

        model.RepositoryText = "https://github.com/owner/app";
        model.AddRepository().ShouldBeTrue();
        model.IsValid.ShouldBeTrue();
        model.CanCreate.ShouldBeFalse("the cost is not confirmed yet");
        model.WhyDisabled.ShouldBe(CloudCopy.CostNotConfirmed);

        model.CostConfirmed = true;
        model.CanCreate.ShouldBeTrue();
        model.WhyDisabled.ShouldBeEmpty();
    }

    /// <summary>A create attempt without the cost confirmation makes no host call at all.</summary>
    [Fact]
    public async Task CreateWithoutTheCostConfirmationMakesNoHostCall()
    {
        var host = CloudFixtures.Host(_clock);
        var model = CloudFixtures.Form(host);

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.NotReady);
        result.Message.ShouldBe(CloudCopy.CostNotConfirmed);
        host.CountOf("Create").ShouldBe(0);
        host.CreateRequests.ShouldBeEmpty();
        model.Result.ShouldBe(result);
    }

    /// <summary>An attempt with an invalid entry makes no host call either, and the result names the problem.</summary>
    [Fact]
    public async Task CreateWithAnInvalidEntryMakesNoHostCall()
    {
        var host = CloudFixtures.Host(_clock);
        var model = CloudFixtures.Form(host);
        model.CostConfirmed = true;
        model.Branch = "not a branch";

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.NotReady);
        result.Message.ShouldBe(CloudCopy.BranchInvalid);
        model.BranchError.ShouldBe(CloudCopy.BranchInvalid);
        host.CountOf("Create").ShouldBe(0);
    }

    /// <summary>The cost sentence says what Railway bills and when idle workers sleep, with the idle time of the form.</summary>
    [Fact]
    public void TheCostNoticeSaysWhatIsBilledAndWhenWorkersSleep()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));

        model.CostNotice.ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 15 minutes.");

        model.IdleMinutesText = "1";
        model.CostNotice.ShouldBe("Railway bills compute while the worker is awake; idle workers sleep after 1 minute.");

        model.IdleMinutesText = "banana";
        model.CostNotice.ShouldContain("15 minutes", Case.Sensitive);
    }

    /// <summary>The confirmation names the idle time, so changing the time withdraws the confirmation.</summary>
    [Fact]
    public void ChangingTheIdleTimeWithdrawsTheCostConfirmation()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        model.CostConfirmed = true;
        model.CanCreate.ShouldBeTrue();

        model.IdleMinutesText = "30";

        model.CostConfirmed.ShouldBeFalse();
        model.CanCreate.ShouldBeFalse();
        model.CostNotice.ShouldContain("30 minutes", Case.Sensitive);
    }

    /// <summary>The request carries the trimmed name, the repositories in order, secret names only, the idle time and null for blank optional entries.</summary>
    [Fact]
    public async Task CreateSendsNamesNeverValues()
    {
        var host = CloudFixtures.Host(_clock);
        var setup = new CloudSetup { IsConfigured = true, KnownSecretNames = ["NPM_TOKEN", "DB_URL"] };
        var model = new NewCloudAgentModel(host, setup) { Name = "  Fix the build  ", Prompt = "  Make CI green.  ", CostConfirmed = true };
        model.RepositoryText = "https://github.com/owner/app";
        _ = model.AddRepository();
        model.RepositoryText = "https://github.com/owner/lib.git";
        _ = model.AddRepository();
        model.SecretNameText = "API_KEY";
        model.AddSecretName().ShouldBeTrue();
        model.AddKnownSecretName("NPM_TOKEN").ShouldBeTrue();
        model.SuggestedSecretNames.ShouldBe(["DB_URL"]);
        CloudAgentCreatedEventArgs? created = null;
        model.Created += (_, args) => created = args;

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.Created);
        result.Message.ShouldBe(CloudCopy.Created("Fix the build"));
        var request = host.CreateRequests.ShouldHaveSingleItem();
        request.Name.ShouldBe("Fix the build");
        request.Repositories.Select(repository => repository.Address).ShouldBe(["https://github.com/owner/app", "https://github.com/owner/lib.git"]);
        request.SecretNames.ShouldBe(["API_KEY", "NPM_TOKEN"]);
        request.IdleMinutes.ShouldBe(15);
        request.Prompt.ShouldBe("Make CI green.");
        request.Branch.ShouldBeNull();
        request.BaseBranch.ShouldBeNull();
        request.EnvironmentId.ShouldBeNull();
        request.Model.ShouldBeNull();
        created.ShouldNotBeNull().Agent.ShouldBe(result.Agent);
        model.CanCreate.ShouldBeFalse("an agent was created; a second click must not create another");
        (await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(result);
        host.CountOf("Create").ShouldBe(1);
    }

    /// <summary>A value pasted into the secret name box is refused, never added, never repeated in the message, and cleared from the box.</summary>
    [Fact]
    public void ASecretValuePastedAsANameIsRefusedAndCleared()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        model.SecretNameText = "API_KEY=" + Canary;

        model.AddSecretName().ShouldBeFalse();

        model.SecretNames.ShouldBeEmpty();
        model.SecretEntryError.ShouldBe(CloudCopy.SecretNameInvalid);
        model.SecretEntryError.ShouldNotContain(Canary, Case.Sensitive);
        model.SecretNameText.ShouldBeEmpty();
        model.WhyDisabled.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A duplicate secret name is refused and the entry stays for editing; the name list holds each name once.</summary>
    [Fact]
    public void ADuplicateSecretNameIsRefused()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        model.SecretNameText = "API_KEY";
        model.AddSecretName().ShouldBeTrue();
        model.SecretNameText = "API_KEY";

        model.AddSecretName().ShouldBeFalse();

        model.SecretEntryError.ShouldBe(CloudCopy.SecretNameDuplicate);
        model.SecretNames.ShouldBe(["API_KEY"]);
        model.RemoveSecretName("API_KEY").ShouldBeTrue();
        model.RemoveSecretName("API_KEY").ShouldBeFalse();
        model.SecretNames.ShouldBeEmpty();
    }

    /// <summary>A refused repository entry keeps its text, shows its sentence, and the sentence goes away as soon as the text changes.</summary>
    [Fact]
    public void ARefusedRepositoryEntryKeepsItsTextAndClearsItsErrorOnEdit()
    {
        var model = new NewCloudAgentModel(CloudFixtures.Host(_clock), new CloudSetup { IsConfigured = true });
        model.RepositoryText = "https://user:" + Canary + "@github.com/owner/app";

        model.AddRepository().ShouldBeFalse();

        model.RepositoryEntryError.ShouldBe(CloudCopy.RepositoryHasCredentials);
        model.RepositoryEntryError.ShouldNotContain(Canary, Case.Sensitive);
        model.Repositories.ShouldBeEmpty();
        model.RepositoryText.ShouldNotBeEmpty();

        model.RepositoryText = "https://github.com/owner/app";
        model.RepositoryEntryError.ShouldBeEmpty();
    }

    /// <summary>The same repository twice (any case, with or without .git) is refused, and a ninth repository is refused.</summary>
    [Fact]
    public void DuplicateRepositoriesAndTooManyRepositoriesAreRefused()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));

        model.RepositoryText = "https://GITHUB.com/Owner/App.git";
        model.AddRepository().ShouldBeFalse();
        model.RepositoryEntryError.ShouldBe(CloudCopy.RepositoryDuplicate);

        for (var index = 2; index <= CloudInputValidator.MaxRepositories; index++)
        {
            model.RepositoryText = $"https://github.com/owner/app{index}";
            model.AddRepository().ShouldBeTrue();
        }

        model.CanAddRepository.ShouldBeFalse();
        model.RepositoryText = "https://github.com/owner/one-too-many";
        model.AddRepository().ShouldBeFalse();
        model.RepositoryEntryError.ShouldBe(CloudCopy.TooManyRepositories(CloudInputValidator.MaxRepositories));
        model.RemoveRepository("https://github.com/owner/app2").ShouldBeTrue();
        model.RemoveRepository("https://github.com/owner/app2").ShouldBeFalse();
    }

    /// <summary>Removing the last repository disables Create again with the repository sentence.</summary>
    [Fact]
    public void RemovingTheLastRepositoryDisablesCreate()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        model.CostConfirmed = true;
        model.CanCreate.ShouldBeTrue();

        model.RemoveRepository("https://github.com/owner/app").ShouldBeTrue();

        model.CanCreate.ShouldBeFalse();
        model.WhyDisabled.ShouldBe(CloudCopy.RepositoryNeeded);
    }

    /// <summary>Each refusal of the service gets its own outcome and shows the service's own words; the dialog stays usable and an edit clears the stale result.</summary>
    [Theory]
    [InlineData(CloudFailureKind.LimitReached, CloudCreateOutcome.LimitReached)]
    [InlineData(CloudFailureKind.QuotaExceeded, CloudCreateOutcome.QuotaExceeded)]
    [InlineData(CloudFailureKind.Rejected, CloudCreateOutcome.Rejected)]
    [InlineData(CloudFailureKind.Conflict, CloudCreateOutcome.Rejected)]
    [InlineData(CloudFailureKind.Unavailable, CloudCreateOutcome.Failed)]
    [InlineData(CloudFailureKind.Unauthorized, CloudCreateOutcome.Failed)]
    [InlineData(CloudFailureKind.NotConfigured, CloudCreateOutcome.Failed)]
    public async Task AServiceRefusalKeepsTheDialogOpenWithItsOutcome(CloudFailureKind kind, CloudCreateOutcome outcome)
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("Create", new CloudHostException(kind, "The service said no."));
        var model = CloudFixtures.Form(host);
        model.CostConfirmed = true;
        var raised = false;
        model.Created += (_, _) => raised = true;

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(outcome);
        result.Message.ShouldBe("The service said no.");
        result.Agent.ShouldBeNull();
        raised.ShouldBeFalse();
        model.IsCreating.ShouldBeFalse();
        model.CanCreate.ShouldBeTrue("the user can change an entry and try again");
        model.Result.ShouldBe(result);

        model.Name = "A different name";
        model.Result.ShouldBeNull();
    }

    /// <summary>A refusal with no text falls back to a plain sentence of its own kind.</summary>
    [Fact]
    public async Task ARefusalWithoutTextFallsBackToAPlainSentence()
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("Create", new CloudHostException(CloudFailureKind.LimitReached, string.Empty));
        var model = CloudFixtures.Form(host);
        model.CostConfirmed = true;

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.LimitReached);
        result.Message.ShouldBe(CloudCopy.LimitReached);
    }

    /// <summary>An exception that is not a host exception reads as a plain failure; its own text, which might hold anything, is not shown.</summary>
    [Fact]
    public async Task AnUnexpectedExceptionNeverShowsItsOwnText()
    {
        var model = new NewCloudAgentModel(new ThrowingCloudHost(new InvalidOperationException("token=" + Canary)), new CloudSetup { IsConfigured = true })
        {
            Name = "Fix the build",
            RepositoryText = "https://github.com/owner/app",
        };
        _ = model.AddRepository();
        model.CostConfirmed = true;

        var result = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudCreateOutcome.Failed);
        result.Message.ShouldBe(CloudCopy.CreateFailed);
        result.Message.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A create the user cancels ends as a failed result with the cancel sentence and leaves the dialog usable.</summary>
    [Fact]
    public async Task ACancelledCreateEndsWithTheCancelSentence()
    {
        var host = CloudFixtures.Host(_clock);
        var release = host.HoldNext("Create");
        var model = CloudFixtures.Form(host);
        model.CostConfirmed = true;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pending = model.CreateAsync(cancel.Token);
        model.IsCreating.ShouldBeTrue();
        await cancel.CancelAsync().ConfigureAwait(true);
        var result = await pending.ConfigureAwait(true);
        release();

        result.Outcome.ShouldBe(CloudCreateOutcome.Failed);
        result.Message.ShouldBe(CloudCopy.CreateCancelled);
        model.IsCreating.ShouldBeFalse();
        model.CanCreate.ShouldBeTrue();
    }

    /// <summary>While a create runs Create and Cancel are locked and a second click makes no second request.</summary>
    [Fact]
    public async Task WhileCreatingASecondCreateIsIgnoredAndCancelIsLocked()
    {
        var host = CloudFixtures.Host(_clock);
        var release = host.HoldNext("Create");
        var model = CloudFixtures.Form(host);
        model.CostConfirmed = true;
        var cancelRequests = 0;
        model.CancelRequested += (_, _) => cancelRequests++;

        var pending = model.CreateAsync(TestContext.Current.CancellationToken);

        model.IsCreating.ShouldBeTrue();
        model.CanCreate.ShouldBeFalse();
        var second = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        second.Outcome.ShouldBe(CloudCreateOutcome.NotReady);
        second.Message.ShouldBe(CloudCopy.CreateInProgress);
        model.Cancel();
        cancelRequests.ShouldBe(0);

        release();
        var first = await pending.ConfigureAwait(true);

        first.Outcome.ShouldBe(CloudCreateOutcome.Created);
        host.CountOf("Create").ShouldBe(1);
        model.Cancel();
        cancelRequests.ShouldBe(1);
    }

    /// <summary>The environment defaults to the one the host names, otherwise the first; it is sent only when the host offers a choice.</summary>
    [Fact]
    public async Task TheEnvironmentDefaultsAndIsSentOnlyWhenThereIsAChoice()
    {
        var environments = new CloudChoice[] { new("dev", "Development"), new("prod", "Production") };
        var named = new NewCloudAgentModel(CloudFixtures.Host(_clock), new CloudSetup { IsConfigured = true, Environments = environments, DefaultEnvironmentId = "prod" });
        var first = new NewCloudAgentModel(CloudFixtures.Host(_clock), new CloudSetup { IsConfigured = true, Environments = environments });
        var none = new NewCloudAgentModel(CloudFixtures.Host(_clock), new CloudSetup { IsConfigured = true });

        named.EnvironmentId.ShouldBe("prod");
        first.EnvironmentId.ShouldBe("dev");
        none.EnvironmentId.ShouldBeNull();
        none.HasEnvironments.ShouldBeFalse();

        var host = CloudFixtures.Host(_clock);
        var model = CloudFixtures.Form(host, new CloudSetup { IsConfigured = true, Environments = environments, Models = [new CloudChoice("fast", "Fast")] });
        model.EnvironmentId = "dev";
        model.ModelId = "fast";
        model.CostConfirmed = true;
        _ = await model.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        host.CreateRequests.ShouldHaveSingleItem().EnvironmentId.ShouldBe("dev");
        host.CreateRequests[0].Model.ShouldBe("fast");

        var plainHost = CloudFixtures.Host(_clock);
        var plain = CloudFixtures.Form(plainHost);
        plain.EnvironmentId = "stale";
        plain.ModelId = "stale";
        plain.CostConfirmed = true;
        _ = await plain.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        plainHost.CreateRequests[0].EnvironmentId.ShouldBeNull();
        plainHost.CreateRequests[0].Model.ShouldBeNull();
    }

    /// <summary>A reached agent cap disables Create with a reason before any request is made.</summary>
    [Fact]
    public void AReachedAgentCapDisablesCreateWithAReason()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock), limits: new CloudLimits(2, 2, 0, null));
        model.CostConfirmed = true;

        model.CanCreate.ShouldBeFalse();
        model.IsValid.ShouldBeFalse();
        model.WhyDisabled.ShouldBe(CloudCopy.AgentCapReached(2));
    }

    /// <summary>A bad idle time, an overlong first task and a bad base branch each disable Create and name themselves.</summary>
    [Fact]
    public void EachBadEntryDisablesCreateAndNamesItself()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        model.CostConfirmed = true;

        model.IdleMinutesText = "0";
        model.IdleError.ShouldBe(CloudCopy.IdleInvalid(1, 240));
        model.CanCreate.ShouldBeFalse();
        model.IdleMinutesText = "15";
        model.CostConfirmed = true;

        model.Prompt = new string('p', CloudInputValidator.MaxPromptLength + 1);
        model.PromptError.ShouldBe(CloudCopy.PromptTooLong(CloudInputValidator.MaxPromptLength));
        model.CanCreate.ShouldBeFalse();
        model.Prompt = string.Empty;

        model.BaseBranch = "two words";
        model.BaseBranchError.ShouldBe(CloudCopy.BranchInvalid);
        model.CanCreate.ShouldBeFalse();
        model.BaseBranch = "main";
        model.CanCreate.ShouldBeTrue();
    }

    /// <summary>A name that is too long shows its error as soon as it is typed; an empty name shows none, because Create's reason already says it.</summary>
    [Fact]
    public void ANameErrorShowsOnlyForANameThatWasTyped()
    {
        var model = new NewCloudAgentModel(CloudFixtures.Host(_clock), new CloudSetup { IsConfigured = true });

        model.NameError.ShouldBeEmpty();
        model.Name = new string('n', CloudInputValidator.MaxNameLength + 1);
        model.NameError.ShouldBe(CloudCopy.NameTooLong(CloudInputValidator.MaxNameLength));
    }

    /// <summary>The model announces the change of the readiness properties a view binds, with the right property names.</summary>
    [Fact]
    public void TheModelAnnouncesWhatAViewBinds()
    {
        var model = CloudFixtures.Form(CloudFixtures.Host(_clock));
        var changed = new List<string>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        model.CostConfirmed = true;
        model.Branch = "feature/x";
        model.SecretNameText = "TOKEN";
        _ = model.AddSecretName();

        changed.ShouldContain(nameof(NewCloudAgentModel.CostConfirmed), StringComparer.Ordinal);
        changed.ShouldContain(nameof(NewCloudAgentModel.CanCreate), StringComparer.Ordinal);
        changed.ShouldContain(nameof(NewCloudAgentModel.Branch), StringComparer.Ordinal);
        changed.ShouldContain(nameof(NewCloudAgentModel.BranchError), StringComparer.Ordinal);
        changed.ShouldContain(nameof(NewCloudAgentModel.SecretNames), StringComparer.Ordinal);
        changed.ShouldContain(nameof(NewCloudAgentModel.SuggestedSecretNames), StringComparer.Ordinal);
    }

    /// <summary>Cancel asks the owner to close the dialog and creates nothing.</summary>
    [Fact]
    public void CancelAsksTheOwnerToCloseTheDialog()
    {
        var host = CloudFixtures.Host(_clock);
        var model = CloudFixtures.Form(host);
        var requested = 0;
        model.CancelRequested += (_, _) => requested++;

        model.Cancel();

        requested.ShouldBe(1);
        host.Calls.ShouldBeEmpty();
    }
}
