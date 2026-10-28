using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Cloud;

/// <summary>The hand-off of a prompt to a new cloud agent, used by the <c>&amp;</c> prefix and the in-cloud command: entries, the cost confirmation and the refusals.</summary>
public sealed class CloudHandoffTests
{
    private const string Canary = "s3cr3t-canary-value";

    private readonly ManualTimeProvider _clock = new();

    private static CloudHandoffRequest Request(string prompt = "Fix the flaky test\nIt fails on CI only.", bool confirmed = true) => new()
    {
        Prompt = prompt,
        Repositories = [CloudFixtures.Repo()],
        CostConfirmed = confirmed,
    };

    /// <summary>A confirmed hand-off creates an agent named after the first line of the prompt, with the whole prompt as its first task and the service's defaults.</summary>
    [Fact]
    public async Task AConfirmedHandoffCreatesTheAgentWithThePromptAsItsFirstTask()
    {
        var host = CloudFixtures.Host(_clock);
        host.Setup = new CloudSetup { IsConfigured = true, DefaultEnvironmentId = "prod", DefaultIdleMinutes = 20, Environments = [new CloudChoice("prod", "Production")] };
        var handoff = new CloudHandoff(host);

        var result = await handoff.StartAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Started);
        result.Message.ShouldBe(CloudCopy.HandoffStarted("Fix the flaky test"));
        result.Agent.ShouldNotBeNull().Name.ShouldBe("Fix the flaky test");
        var request = host.CreateRequests.ShouldHaveSingleItem();
        request.Prompt.ShouldBe("Fix the flaky test\nIt fails on CI only.");
        request.IdleMinutes.ShouldBe(20);
        request.EnvironmentId.ShouldBe("prod");
        request.Repositories.Select(repository => repository.Address).ShouldBe(["https://github.com/owner/app"]);
        request.SecretNames.ShouldBeEmpty();
    }

    /// <summary>Explicit choices of the caller win over the service defaults.</summary>
    [Fact]
    public async Task ExplicitChoicesWinOverTheDefaults()
    {
        var host = CloudFixtures.Host(_clock);
        var handoff = new CloudHandoff(host);

        var result = await handoff.StartAsync(
            Request() with { Name = "Flaky test", Branch = "ompos/flaky", BaseBranch = "main", EnvironmentId = "dev", Model = "fast", IdleMinutes = 5, SecretNames = ["API_KEY"] },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Started);
        var request = host.CreateRequests.ShouldHaveSingleItem();
        request.Name.ShouldBe("Flaky test");
        request.Branch.ShouldBe("ompos/flaky");
        request.BaseBranch.ShouldBe("main");
        request.EnvironmentId.ShouldBe("dev");
        request.Model.ShouldBe("fast");
        request.IdleMinutes.ShouldBe(5);
        request.SecretNames.ShouldBe(["API_KEY"]);
    }

    /// <summary>A long first line becomes a name of at most sixty characters.</summary>
    [Fact]
    public async Task ALongFirstLineBecomesAShortName()
    {
        var host = CloudFixtures.Host(_clock);

        _ = await new CloudHandoff(host).StartAsync(Request(new string('w', 200)), TestContext.Current.CancellationToken).ConfigureAwait(true);

        host.CreateRequests.ShouldHaveSingleItem().Name.Length.ShouldBeLessThanOrEqualTo(60);
        host.CreateRequests[0].Name.Length.ShouldBeGreaterThan(10);
    }

    /// <summary>Without the cost confirmation the hand-off makes no host call, not even a read.</summary>
    [Fact]
    public async Task WithoutTheCostConfirmationNothingIsCalled()
    {
        var host = CloudFixtures.Host(_clock);

        var result = await new CloudHandoff(host).StartAsync(Request(confirmed: false), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsCostConfirmation);
        result.Message.ShouldBe(CloudCopy.CostNotConfirmed);
        result.Agent.ShouldBeNull();
        host.Calls.ShouldBeEmpty();
    }

    /// <summary>A missing prompt, a missing or bad repository, a bad secret name, a bad branch or a bad idle time are refused before any call, each with its own sentence.</summary>
    [Fact]
    public async Task BadEntriesAreRefusedBeforeAnyCall()
    {
        var host = CloudFixtures.Host(_clock);
        var handoff = new CloudHandoff(host);
        var token = TestContext.Current.CancellationToken;

        (await handoff.StartAsync(Request("   "), token).ConfigureAwait(true)).Message.ShouldBe(CloudCopy.HandoffNeedsPrompt);
        (await handoff.StartAsync(Request() with { Repositories = [] }, token).ConfigureAwait(true)).Message.ShouldBe(CloudCopy.RepositoryNeeded);
        (await handoff.StartAsync(Request() with { Repositories = [new CloudRepository("https://user:" + Canary + "@github.com/owner/app")] }, token).ConfigureAwait(true))
            .Message.ShouldBe(CloudCopy.RepositoryHasCredentials);
        (await handoff.StartAsync(Request() with { Repositories = [new CloudRepository("http://github.com/owner/app")] }, token).ConfigureAwait(true))
            .Message.ShouldBe(CloudCopy.RepositoryNotHttps);
        (await handoff.StartAsync(Request() with { SecretNames = ["API_KEY=" + Canary] }, token).ConfigureAwait(true)).Message.ShouldBe(CloudCopy.SecretNameInvalid);
        (await handoff.StartAsync(Request() with { Branch = "bad branch" }, token).ConfigureAwait(true)).Message.ShouldBe(CloudCopy.BranchInvalid);
        (await handoff.StartAsync(Request(new string('p', CloudInputValidator.MaxPromptLength + 1)), token).ConfigureAwait(true))
            .Message.ShouldBe(CloudCopy.PromptTooLong(CloudInputValidator.MaxPromptLength));

        host.Calls.ShouldBeEmpty();
    }

    /// <summary>Every refused entry reads as needing input, and no sentence repeats a credential.</summary>
    [Fact]
    public async Task RefusedEntriesNeedInputAndNeverRepeatACredential()
    {
        var host = CloudFixtures.Host(_clock);

        var result = await new CloudHandoff(host).StartAsync(
            Request() with { Repositories = [new CloudRepository("https://" + Canary + "@github.com/owner/app")] },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsInput);
        result.Message.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>An idle time outside the service's bounds is refused after the setup was read, and nothing is created.</summary>
    [Fact]
    public async Task AnIdleTimeOutsideTheBoundsIsRefused()
    {
        var host = CloudFixtures.Host(_clock);

        var result = await new CloudHandoff(host).StartAsync(Request() with { IdleMinutes = 241 }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NeedsInput);
        result.Message.ShouldBe(CloudCopy.IdleInvalid(1, 240));
        host.CountOf("Create").ShouldBe(0);
    }

    /// <summary>A host that is not set up makes no create and says why.</summary>
    [Fact]
    public async Task ANotConfiguredHostIsNotAvailable()
    {
        var host = CloudFixtures.Host(_clock);
        host.Setup = CloudSetup.NotConfigured("Sign in to the OmpOS service first.");

        var result = await new CloudHandoff(host).StartAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.NotAvailable);
        result.Message.ShouldBe("Sign in to the OmpOS service first.");
        host.CountOf("Create").ShouldBe(0);
    }

    /// <summary>Each refusal of the service has its own outcome and shows the service's words.</summary>
    [Theory]
    [InlineData(CloudFailureKind.LimitReached, CloudHandoffOutcome.LimitReached)]
    [InlineData(CloudFailureKind.QuotaExceeded, CloudHandoffOutcome.QuotaExceeded)]
    [InlineData(CloudFailureKind.Rejected, CloudHandoffOutcome.Rejected)]
    [InlineData(CloudFailureKind.NotFound, CloudHandoffOutcome.Rejected)]
    [InlineData(CloudFailureKind.Unavailable, CloudHandoffOutcome.Failed)]
    [InlineData(CloudFailureKind.Unauthorized, CloudHandoffOutcome.NotAvailable)]
    [InlineData(CloudFailureKind.NotConfigured, CloudHandoffOutcome.NotAvailable)]
    public async Task AServiceRefusalHasItsOwnOutcome(CloudFailureKind kind, CloudHandoffOutcome outcome)
    {
        var host = CloudFixtures.Host(_clock);
        host.FailNext("Create", new CloudHostException(kind, "The service said no."));

        var result = await new CloudHandoff(host).StartAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(outcome);
        result.Message.ShouldBe("The service said no.");
        result.Agent.ShouldBeNull();
    }

    /// <summary>An exception the host did not translate reads as the plain failure sentence.</summary>
    [Fact]
    public async Task AnUntranslatedFailureReadsAsAPlainFailure()
    {
        var result = await new CloudHandoff(new ThrowingCloudHost(new InvalidOperationException("token=" + Canary)))
            .StartAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Failed);
        result.Message.ShouldBe(CloudCopy.CreateFailed);
        result.Message.ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A cancelled hand-off ends as a failure with the cancel sentence.</summary>
    [Fact]
    public async Task ACancelledHandoffEndsWithTheCancelSentence()
    {
        var host = CloudFixtures.Host(_clock);
        _ = host.HoldNext("Create");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var pending = new CloudHandoff(host).StartAsync(Request(), cancel.Token);
        await cancel.CancelAsync().ConfigureAwait(true);
        var result = await pending.ConfigureAwait(true);

        result.Outcome.ShouldBe(CloudHandoffOutcome.Failed);
        result.Message.ShouldBe(CloudCopy.CreateCancelled);
    }
}
