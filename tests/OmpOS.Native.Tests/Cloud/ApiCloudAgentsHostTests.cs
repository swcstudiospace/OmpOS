using System.Net;
using System.Text.Json;
using OmpOS.Contracts.Cloud;
using OmpOS.Native.Views;
using OmpOS.Presentation.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Cloud;

/// <summary>
/// The desktop's cloud host against a scripted control plane: the route and body of every call, the mapping of the wire agent, the failure kinds
/// by status and problem code, and the <c>Changes</c> signal after a call and from the poll. No socket is opened.
/// </summary>
public sealed class ApiCloudAgentsHostTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static CloudAgentResponse Wire(
        string id = "agent-1",
        Contracts.Cloud.CloudAgentStatus status = Contracts.Cloud.CloudAgentStatus.Running,
        IReadOnlyList<CloudPullRequest>? pullRequests = null,
        CloudAgentError? error = null) => new()
    {
        Id = id,
        Name = "Fix the build",
        Status = status,
        CreatedAt = Origin,
        Repositories = [new Contracts.Cloud.CloudRepository { Url = "https://github.com/owner/app", BaseBranch = "main" }],
        Branch = "ompos/agent-1",
        SessionId = "cloud-" + id,
        PullRequests = pullRequests ?? [],
        LastActivityAt = Origin.AddMinutes(5),
        IdleMinutes = 15,
        ComputeSeconds = 12.5,
        SecretRefs = [],
        Model = "model-a",
        Error = error,
    };

    private static string Serialize(CloudAgentResponse agent) => JsonSerializer.Serialize(agent, CloudJsonContext.Default.CloudAgentResponse);

    private static string SerializeList(params CloudAgentResponse[] agents) =>
        JsonSerializer.Serialize(new CloudAgentListResponse { Agents = agents }, CloudJsonContext.Default.CloudAgentListResponse);

    private static string SerializeLimits(int maxAgents, int agents, int running, int defaultIdle) =>
        JsonSerializer.Serialize(
            new CloudLimitsResponse
            {
                MaxAgents = maxAgents,
                MaxConcurrentRunning = 2,
                Agents = agents,
                Running = running,
                DefaultIdleMinutes = defaultIdle,
                MaxComputeSecondsPerAgent = 0,
                ComputeSeconds = 0,
            },
            CloudJsonContext.Default.CloudLimitsResponse);

    /// <summary>Without an API address the host is not configured: the setup says so, nothing is sent, and every other call fails with that kind.</summary>
    [Fact]
    public async Task WithoutAnApiAddressTheHostIsNotConfiguredAndSendsNothing()
    {
        using var rig = new CloudHostRig(static _ => throw new InvalidOperationException("no request may be sent"), useDefaultApi: false);

        var setup = await rig.Host.GetSetupAsync(Cancel).ConfigureAwait(true);
        var failure = await Should.ThrowAsync<CloudHostException>(() => rig.Host.ListAsync(Cancel)).ConfigureAwait(true);

        setup.IsConfigured.ShouldBeFalse();
        setup.NotConfiguredReason.ShouldBe(CloudCopy.NotConfiguredIntro, Case.Sensitive);
        failure.Kind.ShouldBe(CloudFailureKind.NotConfigured);
        rig.Calls.ShouldBeEmpty();
    }

    /// <summary>An API address with a path prefix keeps its prefix, the bearer token is sent, and the wire agent is mapped field by field.</summary>
    [Fact]
    public async Task ListMapsTheWireAgentAndSendsTheBearerToken()
    {
        var pullRequests = new[]
        {
            new CloudPullRequest { Repository = "owner/app", Number = 7, Url = "https://github.com/owner/app/pull/7", Branch = "ompos/agent-1", BaseBranch = "main", Draft = true, CreatedAt = Origin },
            new CloudPullRequest { Repository = "owner/app", Number = 8, Url = "http://insecure.example/pull/8", Branch = "ompos/agent-1", BaseBranch = "main", Draft = true, CreatedAt = Origin },
        };
        var body = SerializeList(Wire(pullRequests: pullRequests, error: new CloudAgentError { Code = "x", Message = "The worker did not wake in time." }));
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Json(HttpStatusCode.OK, body), api: new Uri("https://host.example/api", UriKind.Absolute));

        var agents = await rig.Host.ListAsync(Cancel).ConfigureAwait(true);

        var call = rig.Calls.ShouldHaveSingleItem();
        call.Url.AbsoluteUri.ShouldBe("https://host.example/api/cloud/agents", Case.Sensitive);
        call.Authorization.ShouldBe("Bearer " + CloudHostRig.BearerToken, Case.Sensitive);
        var agent = agents.ShouldHaveSingleItem();
        agent.Id.ShouldBe("agent-1", Case.Sensitive);
        agent.Status.ShouldBe(Presentation.Cloud.CloudAgentStatus.Running);
        agent.Branch.ShouldBe("ompos/agent-1", Case.Sensitive);
        agent.BaseBranch.ShouldBe("main", Case.Sensitive);
        agent.SessionId.ShouldBe("cloud-agent-1", Case.Sensitive);
        agent.IdleMinutes.ShouldBe(15);
        agent.Model.ShouldBe("model-a", Case.Sensitive);
        agent.StatusDetail.ShouldBe("The worker did not wake in time.", Case.Sensitive);
        agent.Repositories.Select(static repository => repository.Url).ToArray().ShouldBe(["https://github.com/owner/app"]);
        agent.PullRequests.ShouldHaveSingleItem().Url.AbsoluteUri.ShouldBe("https://github.com/owner/app/pull/7", Case.Sensitive);
    }

    /// <summary>A user who is not signed in sends no authorization header; the API's 401 comes back as the unauthorized kind.</summary>
    [Fact]
    public async Task WithoutATokenNoAuthorizationHeaderIsSentAndA401IsUnauthorized()
    {
        using var rig = new CloudHostRig(static _ => ScriptedCloudHandler.Problem(HttpStatusCode.Unauthorized, "unauthorized", "Sign in first."), signedIn: false);

        var failure = await Should.ThrowAsync<CloudHostException>(() => rig.Host.ListAsync(Cancel)).ConfigureAwait(true);

        rig.Calls.ShouldHaveSingleItem().Authorization.ShouldBeNull();
        failure.Kind.ShouldBe(CloudFailureKind.Unauthorized);
        failure.Message.ShouldBe("Sign in first.", Case.Sensitive);
    }

    /// <summary>Each status and problem code maps to one failure kind, and the message is the problem's detail without the token or the address.</summary>
    [Theory]
    [InlineData(403, "forbidden", CloudFailureKind.Unauthorized)]
    [InlineData(404, "agent_not_found", CloudFailureKind.NotFound)]
    [InlineData(409, "agent_not_stopped", CloudFailureKind.Conflict)]
    [InlineData(400, "invalid_request", CloudFailureKind.Rejected)]
    [InlineData(413, "too_large", CloudFailureKind.Rejected)]
    [InlineData(429, "agent_limit_reached", CloudFailureKind.LimitReached)]
    [InlineData(429, "running_limit_reached", CloudFailureKind.LimitReached)]
    [InlineData(429, "compute_limit_reached", CloudFailureKind.QuotaExceeded)]
    [InlineData(502, "bad_gateway", CloudFailureKind.Unavailable)]
    [InlineData(503, "unavailable", CloudFailureKind.Unavailable)]
    public async Task FailureKindsFollowTheStatusAndTheProblemCode(int status, string code, CloudFailureKind expected)
    {
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Problem((HttpStatusCode)status, code, "Plain sentence from the service."));

        var failure = await Should.ThrowAsync<CloudHostException>(() => rig.Host.WakeAsync("agent-1", Cancel)).ConfigureAwait(true);

        failure.Kind.ShouldBe(expected);
        failure.Message.ShouldBe("Plain sentence from the service.", Case.Sensitive);
        failure.Message.ShouldNotContain(CloudHostRig.BearerToken, Case.Sensitive);
        failure.Message.ShouldNotContain("api.example.test", Case.Sensitive);
    }

    /// <summary>A failure that carries no problem document gets a fixed sentence for its kind, never the response body.</summary>
    [Fact]
    public async Task AFailureWithoutAProblemBodyGetsTheFixedSentence()
    {
        using var rig = new CloudHostRig(static _ => ScriptedCloudHandler.Json(HttpStatusCode.InternalServerError, "<html>stack trace with a secret</html>"));

        var failure = await Should.ThrowAsync<CloudHostException>(() => rig.Host.ListAsync(Cancel)).ConfigureAwait(true);

        failure.Kind.ShouldBe(CloudFailureKind.Unavailable);
        failure.Message.ShouldNotContain("secret", Case.Insensitive);
        failure.Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A transport failure and a timeout are the unavailable kind and keep the exception text out of the message.</summary>
    [Fact]
    public async Task TransportFailuresAreUnavailable()
    {
        using var refused = new CloudHostRig(static _ => throw new HttpRequestException("connection refused to 10.0.0.7"));
        using var timedOut = new CloudHostRig(static _ => throw new TaskCanceledException("timed out"));

        var refusal = await Should.ThrowAsync<CloudHostException>(() => refused.Host.ListAsync(Cancel)).ConfigureAwait(true);
        var timeout = await Should.ThrowAsync<CloudHostException>(() => timedOut.Host.ListAsync(Cancel)).ConfigureAwait(true);

        refusal.Kind.ShouldBe(CloudFailureKind.Unavailable);
        refusal.Message.ShouldNotContain("10.0.0.7", Case.Sensitive);
        timeout.Kind.ShouldBe(CloudFailureKind.Unavailable);
    }

    /// <summary>A cancellation the caller asked for stays a cancellation: it is not reported as a service failure.</summary>
    [Fact]
    public async Task CallerCancellationIsNotAServiceFailure()
    {
        using var rig = new CloudHostRig(static _ => ScriptedCloudHandler.Json(HttpStatusCode.OK, "{\"agents\":[]}"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => rig.Host.ListAsync(cancelled.Token)).ConfigureAwait(true);
    }

    /// <summary>Create sends every repository with the branch pair, the secret names and the idle time, then sends the first task as a prompt.</summary>
    [Fact]
    public async Task CreateSendsTheRequestAndThenTheFirstTask()
    {
        using var rig = new CloudHostRig(call => call.PathAndQuery.EndsWith("/prompts", StringComparison.Ordinal)
            ? ScriptedCloudHandler.Json(HttpStatusCode.Accepted, "{\"agentId\":\"agent-1\",\"sessionId\":\"cloud-agent-1\",\"status\":\"running\"}")
            : ScriptedCloudHandler.Json(HttpStatusCode.Accepted, Serialize(Wire(status: Contracts.Cloud.CloudAgentStatus.Provisioning))));

        var record = await rig.Host.CreateAsync(
            new CloudAgentRequest
            {
                Name = "Fix the build",
                Repositories = [new Presentation.Cloud.CloudRepository("https://github.com/owner/app"), new Presentation.Cloud.CloudRepository("https://github.com/owner/lib")],
                Branch = "work",
                BaseBranch = "develop",
                SecretNames = ["NPM_TOKEN"],
                IdleMinutes = 30,
                Model = "model-a",
                Prompt = "Make the tests pass.",
            },
            Cancel).ConfigureAwait(true);

        rig.Calls.Count.ShouldBe(2);
        var create = rig.Calls[0];
        create.Method.ShouldBe(HttpMethod.Post);
        create.PathAndQuery.ShouldBe("/cloud/agents", Case.Sensitive);
        using var body = JsonDocument.Parse(create.Body);
        body.RootElement.GetProperty("name").GetString().ShouldBe("Fix the build", Case.Sensitive);
        body.RootElement.GetProperty("repositories").GetArrayLength().ShouldBe(2);
        body.RootElement.GetProperty("repositories")[0].GetProperty("branch").GetString().ShouldBe("work", Case.Sensitive);
        body.RootElement.GetProperty("repositories")[1].GetProperty("baseBranch").GetString().ShouldBe("develop", Case.Sensitive);
        body.RootElement.GetProperty("secretRefs")[0].GetString().ShouldBe("NPM_TOKEN", Case.Sensitive);
        body.RootElement.GetProperty("idleMinutes").GetInt32().ShouldBe(30);
        body.RootElement.GetProperty("model").GetString().ShouldBe("model-a", Case.Sensitive);
        var prompt = rig.Calls[1];
        prompt.PathAndQuery.ShouldBe("/cloud/agents/agent-1/prompts", Case.Sensitive);
        prompt.Body.ShouldContain("Make the tests pass.", Case.Sensitive);
        record.Status.ShouldBe(Presentation.Cloud.CloudAgentStatus.Provisioning);
        record.StatusDetail.ShouldBeNull();
    }

    /// <summary>A first task that cannot be sent does not hide the agent that was created: the record comes back and says what happened.</summary>
    [Fact]
    public async Task AFailedFirstTaskKeepsTheCreatedAgent()
    {
        using var rig = new CloudHostRig(call => call.PathAndQuery.EndsWith("/prompts", StringComparison.Ordinal)
            ? ScriptedCloudHandler.Problem(HttpStatusCode.Conflict, "agent_not_ready", "Not ready yet.")
            : ScriptedCloudHandler.Json(HttpStatusCode.Accepted, Serialize(Wire(status: Contracts.Cloud.CloudAgentStatus.Provisioning))));

        var record = await rig.Host.CreateAsync(
            new CloudAgentRequest { Name = "n", Repositories = [new Presentation.Cloud.CloudRepository("https://github.com/owner/app")], Prompt = "Go." },
            Cancel).ConfigureAwait(true);

        record.Id.ShouldBe("agent-1", Case.Sensitive);
        record.StatusDetail.ShouldBe("The agent was created but its first task could not be sent.", Case.Sensitive);
    }

    /// <summary>Wake and stop post to their routes and return the agent the API answers with; delete uses DELETE and an escaped id.</summary>
    [Fact]
    public async Task WakeStopAndDeleteUseTheirRoutes()
    {
        using var rig = new CloudHostRig(call => call.Method == HttpMethod.Delete
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : ScriptedCloudHandler.Json(
                HttpStatusCode.OK,
                Serialize(Wire(status: call.PathAndQuery.EndsWith("/stop", StringComparison.Ordinal) ? Contracts.Cloud.CloudAgentStatus.Stopping : Contracts.Cloud.CloudAgentStatus.Starting))));

        var woken = await rig.Host.WakeAsync("agent-1", Cancel).ConfigureAwait(true);
        var stopped = await rig.Host.StopAsync("agent-1", Cancel).ConfigureAwait(true);
        await rig.Host.DeleteAsync("agent 1/x", Cancel).ConfigureAwait(true);

        woken.Status.ShouldBe(Presentation.Cloud.CloudAgentStatus.Starting);
        stopped.Status.ShouldBe(Presentation.Cloud.CloudAgentStatus.Stopping);
        rig.Calls.Select(static call => $"{call.Method} {call.PathAndQuery}").Take(2).ToArray().ShouldBe(
            ["POST /cloud/agents/agent-1/wake", "POST /cloud/agents/agent-1/stop"]);
        rig.Calls[2].Method.ShouldBe(HttpMethod.Delete);
        rig.Calls[2].Url.AbsoluteUri.ShouldEndWith("/cloud/agents/agent%201%2Fx", Case.Sensitive);
    }

    /// <summary>Setup reads the limits and the secret names (names only, sorted).</summary>
    [Fact]
    public async Task SetupReadsTheSecretNamesOnly()
    {
        var limits = SerializeLimits(maxAgents: 5, agents: 1, running: 0, defaultIdle: 20);
        var secrets = JsonSerializer.Serialize(
            new CloudSecretListResponse { Secrets = [new CloudSecretInfo { Name = "B", UpdatedAt = Origin }, new CloudSecretInfo { Name = "A", UpdatedAt = Origin }] },
            CloudJsonContext.Default.CloudSecretListResponse);
        using var rig = new CloudHostRig(call => ScriptedCloudHandler.Json(HttpStatusCode.OK, call.PathAndQuery.EndsWith("/limits", StringComparison.Ordinal) ? limits : secrets));

        var setup = await rig.Host.GetSetupAsync(Cancel).ConfigureAwait(true);

        setup.IsConfigured.ShouldBeTrue();
        setup.KnownSecretNames.ToArray().ShouldBe(["A", "B"]);
        setup.DefaultIdleMinutes.ShouldBe(20);
        rig.Calls.Select(static call => call.PathAndQuery).Order(StringComparer.Ordinal).ToArray().ShouldBe(["/cloud/limits", "/cloud/secrets"]);
    }

    /// <summary>The API maps no cloud routes when cloud agents are off: a 404 on the limits means the installation is not set up.</summary>
    [Fact]
    public async Task AMissingLimitsRouteMeansNotConfigured()
    {
        using var rig = new CloudHostRig(static _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var setup = await rig.Host.GetSetupAsync(Cancel).ConfigureAwait(true);

        setup.IsConfigured.ShouldBeFalse();
    }

    /// <summary>A cap of zero on the wire means no cap; the used counts pass through.</summary>
    [Fact]
    public async Task LimitsMapZeroToNoCap()
    {
        var limits = SerializeLimits(maxAgents: 0, agents: 3, running: 1, defaultIdle: 15);
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Json(HttpStatusCode.OK, limits));

        var result = await rig.Host.GetLimitsAsync(Cancel).ConfigureAwait(true);

        result.Agents.ShouldBe(3);
        result.MaxAgents.ShouldBeNull();
        result.AwakeAgents.ShouldBe(1);
        result.MaxAwakeAgents.ShouldBe(2);
    }

    /// <summary>The pull request route carries the request and returns a link.</summary>
    [Fact]
    public async Task CreatePullRequestSendsTheRequestAndReturnsTheLink()
    {
        var created = JsonSerializer.Serialize(
            new CloudPullRequest { Repository = "owner/app", Number = 9, Url = "https://github.com/owner/app/pull/9", Branch = "b", BaseBranch = "main", Draft = true, CreatedAt = Origin },
            CloudJsonContext.Default.CloudPullRequest);
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Json(HttpStatusCode.Created, created));

        var link = await rig.Host.CreatePullRequestAsync(
            "agent-1",
            new CloudPullRequestRequest { Repository = "owner/app", Title = "Fix", Body = "Why", BaseBranch = "main", Draft = false },
            Cancel).ConfigureAwait(true);

        link.Url.AbsoluteUri.ShouldBe("https://github.com/owner/app/pull/9", Case.Sensitive);
        link.Title.ShouldBe("Fix", Case.Sensitive);
        var call = rig.Calls.ShouldHaveSingleItem();
        call.PathAndQuery.ShouldBe("/cloud/agents/agent-1/pull-request", Case.Sensitive);
        using var body = JsonDocument.Parse(call.Body);
        body.RootElement.GetProperty("draft").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("baseBranch").GetString().ShouldBe("main", Case.Sensitive);
        body.RootElement.GetProperty("repository").GetString().ShouldBe("owner/app", Case.Sensitive);
    }

    /// <summary>An address that is not https cannot be handed to the browser, so a pull request that has one is refused.</summary>
    [Fact]
    public async Task APullRequestWithAnInsecureAddressIsRefused()
    {
        var created = JsonSerializer.Serialize(
            new CloudPullRequest { Repository = "owner/app", Number = 9, Url = "http://github.com/owner/app/pull/9", Branch = "b", BaseBranch = "main", Draft = true, CreatedAt = Origin },
            CloudJsonContext.Default.CloudPullRequest);
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Json(HttpStatusCode.Created, created));

        var failure = await Should.ThrowAsync<CloudHostException>(
            () => rig.Host.CreatePullRequestAsync("agent-1", new CloudPullRequestRequest { Repository = "owner/app", Title = "Fix" }, Cancel)).ConfigureAwait(true);

        failure.Kind.ShouldBe(CloudFailureKind.Rejected);
    }

    /// <summary>Opening a page goes through the launcher and only for https; a launcher that fails is the unavailable kind.</summary>
    [Fact]
    public async Task OpeningAPullRequestUsesTheLauncherForHttpsOnly()
    {
        var launcher = new RecordingLauncher(result: true);
        var failing = new RecordingLauncher(result: false);
        using var withLauncher = new CloudHostRig(static _ => new HttpResponseMessage(HttpStatusCode.OK), launcher: launcher);
        using var withFailingLauncher = new CloudHostRig(static _ => new HttpResponseMessage(HttpStatusCode.OK), launcher: failing);
        using var withoutLauncher = new CloudHostRig(static _ => new HttpResponseMessage(HttpStatusCode.OK));
        var secure = new CloudPullRequestLink("owner/app", new Uri("https://github.com/owner/app/pull/1"));
        var insecure = new CloudPullRequestLink("owner/app", new Uri("http://github.com/owner/app/pull/1"));

        await withLauncher.Host.OpenPullRequestAsync(secure, Cancel).ConfigureAwait(true);
        var plain = await Should.ThrowAsync<CloudHostException>(() => withLauncher.Host.OpenPullRequestAsync(insecure, Cancel)).ConfigureAwait(true);
        var noBrowser = await Should.ThrowAsync<CloudHostException>(() => withFailingLauncher.Host.OpenPullRequestAsync(secure, Cancel)).ConfigureAwait(true);
        var noLauncher = await Should.ThrowAsync<CloudHostException>(() => withoutLauncher.Host.OpenPullRequestAsync(secure, Cancel)).ConfigureAwait(true);

        launcher.Opened.ShouldHaveSingleItem().AbsoluteUri.ShouldBe("https://github.com/owner/app/pull/1", Case.Sensitive);
        plain.Kind.ShouldBe(CloudFailureKind.Rejected);
        noBrowser.Kind.ShouldBe(CloudFailureKind.Unavailable);
        noLauncher.Kind.ShouldBe(CloudFailureKind.Unavailable);
        withLauncher.Calls.ShouldBeEmpty();
    }

    /// <summary>A call that changes an agent raises <c>Changes</c> once; a read does not.</summary>
    [Fact]
    public async Task ChangesIsRaisedAfterAMutatingCallOnly()
    {
        using var rig = new CloudHostRig(call => call.Method == HttpMethod.Get
            ? ScriptedCloudHandler.Json(HttpStatusCode.OK, SerializeList(Wire()))
            : ScriptedCloudHandler.Json(HttpStatusCode.OK, Serialize(Wire())));
        var raised = 0;
        rig.Host.Changes += (_, _) => raised++;

        _ = await rig.Host.ListAsync(Cancel).ConfigureAwait(true);
        raised.ShouldBe(0);

        _ = await rig.Host.WakeAsync("agent-1", Cancel).ConfigureAwait(true);
        raised.ShouldBe(1);
    }

    /// <summary>The first poll only records the baseline; a later poll raises <c>Changes</c> when an agent's status changed and not otherwise.</summary>
    [Fact]
    public async Task ThePollRaisesChangesOnlyWhenAnAgentChanged()
    {
        var status = Contracts.Cloud.CloudAgentStatus.Running;
        using var rig = new CloudHostRig(_ => ScriptedCloudHandler.Json(HttpStatusCode.OK, SerializeList(Wire(status: status))));
        var raised = 0;
        rig.Host.Changes += (_, _) => raised++;

        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        raised.ShouldBe(0);

        status = Contracts.Cloud.CloudAgentStatus.Idle;
        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        raised.ShouldBe(1);

        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        raised.ShouldBe(1);
    }

    /// <summary>A poll that fails keeps the baseline and raises nothing; the next successful poll decides.</summary>
    [Fact]
    public async Task AFailedPollRaisesNothing()
    {
        var failing = false;
        using var rig = new CloudHostRig(_ => failing
            ? ScriptedCloudHandler.Problem(HttpStatusCode.ServiceUnavailable, "unavailable", "Try later.")
            : ScriptedCloudHandler.Json(HttpStatusCode.OK, SerializeList(Wire())));
        var raised = 0;
        rig.Host.Changes += (_, _) => raised++;

        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        failing = true;
        await rig.Host.PollOnceAsync().ConfigureAwait(true);
        failing = false;
        await rig.Host.PollOnceAsync().ConfigureAwait(true);

        raised.ShouldBe(0);
    }

    /// <summary>The poll timer exists only while a subscriber is attached, and each tick lists the agents.</summary>
    [Fact]
    public async Task ThePollTimerRunsOnlyWhileSomeoneListens()
    {
        var clock = new CloudPollClock();
        using var rig = new CloudHostRig(static _ => ScriptedCloudHandler.Json(HttpStatusCode.OK, "{\"agents\":[]}"), clock: clock);
        void OnChanges(object? sender, EventArgs e)
        {
        }

        clock.ArmedTimers.ShouldBe(0);
        rig.Host.Changes += OnChanges;
        clock.ArmedTimers.ShouldBe(1);

        clock.Tick();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (rig.Calls.Count == 0)
        {
            await Task.Yield();
            timeout.Token.ThrowIfCancellationRequested();
        }

        rig.Calls[0].PathAndQuery.ShouldBe("/cloud/agents", Case.Sensitive);
        rig.Host.Changes -= OnChanges;
        clock.ArmedTimers.ShouldBe(0);
    }

    private sealed class RecordingLauncher(bool result) : IUrlLauncher
    {
        public List<Uri> Opened { get; } = [];

        public Task<bool> OpenAsync(Uri uri, CancellationToken cancellationToken)
        {
            Opened.Add(uri);
            return Task.FromResult(result);
        }
    }
}
