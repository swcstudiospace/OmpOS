using System.Net;
using System.Text.Json;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>Git credentials and pull requests against a fake GitHub: the token request (repositories and permissions), refresh before expiry, the pull request payload, the draft rules, the default-branch refusal, the PAT fallback, the environment file, and the canary proof that a token is never in a response, an event or a log line.</summary>
public sealed class CloudPullRequestTests
{
    private const string PatValue = "github_pat_fake_personal_token_0123456789";

    /// <summary>The agent's branch is opened as a pull request ready for review, with a token limited to the one repository and the least permissions the step needs.</summary>
    [Fact]
    public async Task PullRequest_IsOpenedReadyForReviewWithAScopedToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, branch, StringComparison.Ordinal);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{id}/pull-request", "{\"title\":\"Add readme\",\"body\":\"Written by the agent.\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pull = await CloudApiHost.ReadAsync(response, ct).ConfigureAwait(true);
        pull.GetProperty("number").GetInt32().ShouldBe(7);
        pull.GetProperty("url").GetString().ShouldBe("https://github.test/acme/widgets/pull/7");
        pull.GetProperty("branch").GetString().ShouldBe(branch);
        pull.GetProperty("baseBranch").GetString().ShouldBe("main");
        pull.GetProperty("draft").GetBoolean().ShouldBeFalse();
        var create = host.GitHub.Requests.Single(static request => string.Equals(request.Method, "POST", StringComparison.Ordinal) && string.Equals(request.Path, "/repos/acme/widgets/pulls", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(create.Body);
        body.RootElement.GetProperty("title").GetString().ShouldBe("Add readme");
        body.RootElement.GetProperty("head").GetString().ShouldBe(branch);
        body.RootElement.GetProperty("base").GetString().ShouldBe("main");
        body.RootElement.GetProperty("draft").GetBoolean().ShouldBeFalse();
        create.Authorization.ShouldStartWith("Bearer ghs_fake_installation_token_", Case.Sensitive);

        var mints = MintBodies(host);
        mints.All(static mint => mint.GetProperty("repositories").GetArrayLength() == 1).ShouldBeTrue();
        mints.All(static mint => string.Equals(mint.GetProperty("repositories")[0].GetString(), "widgets", StringComparison.Ordinal)).ShouldBeTrue();
        mints.Any(static mint => string.Equals(Permission(mint, "contents"), "write", StringComparison.Ordinal) && string.Equals(Permission(mint, "pull_requests"), "write", StringComparison.Ordinal)).ShouldBeTrue();
        mints.Any(static mint => string.Equals(Permission(mint, "contents"), "read", StringComparison.Ordinal) && string.Equals(Permission(mint, "pull_requests"), "write", StringComparison.Ordinal)).ShouldBeTrue();
        using var read = await client.GetAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        var stored = await CloudApiHost.ReadAsync(read, ct).ConfigureAwait(true);
        stored.GetProperty("pullRequest").GetProperty("number").GetInt32().ShouldBe(7);
        stored.GetProperty("pullRequests").GetArrayLength().ShouldBe(1);
    }

    /// <summary>A draft is opened only when the caller asks for one; the NeverDraft setting overrides the request.</summary>
    [Theory]
    [InlineData("false", "true", true)]
    [InlineData("true", "true", false)]
    [InlineData("false", "false", false)]
    public async Task Draft_FollowsTheRequestAndTheNeverDraftSetting(string neverDraft, string requested, bool expectedDraft)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Git:PullRequests:NeverDraft"] = neverDraft }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, branch, StringComparison.Ordinal);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\",\"draft\":" + requested + "}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await CloudApiHost.ReadAsync(response, ct).ConfigureAwait(true)).GetProperty("draft").GetBoolean().ShouldBe(expectedDraft);
        var create = host.GitHub.Requests.Single(static request => string.Equals(request.Method, "POST", StringComparison.Ordinal) && string.Equals(request.Path, "/repos/acme/widgets/pulls", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(create.Body);
        body.RootElement.GetProperty("draft").GetBoolean().ShouldBe(expectedDraft);
    }

    /// <summary>A pull request needs the branch on the host: before the agent pushed it, the request is a conflict and nothing is opened.</summary>
    [Fact]
    public async Task BranchNotPushed_IsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe("branch_not_pushed");
        host.GitHub.Requests.Any(static request => request.Path.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>An agent never opens a pull request from the default branch (or into itself): the request is refused before GitHub is asked to open anything.</summary>
    [Fact]
    public async Task DefaultBranch_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.DefaultBranch = branch;
        host.GitHub.BranchExists = static _ => true;
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe("branch_is_default");
        host.GitHub.Requests.Any(static request => string.Equals(request.Method, "POST", StringComparison.Ordinal) && request.Path.EndsWith("/pulls", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>The worker is told which branches are protected, so its push guard can refuse them.</summary>
    [Fact]
    public async Task Provisioning_ListsTheProtectedDefaultBranch()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.DefaultBranch = "trunk";

        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        var variables = host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull();
        variables["AUTONOGRAMMER_PROTECTED_BRANCHES"].ShouldBe("[\"trunk\"]");
        variables["AUTONOGRAMMER_GIT_TOKEN"].ShouldBe(FakeGitHubHandler.TokenOf(1));
        variables["GIT_TERMINAL_PROMPT"].ShouldBe("0");
    }

    /// <summary>A second pull request for the same repository is a conflict, both from the record and when GitHub says one already exists.</summary>
    [Fact]
    public async Task SecondPullRequest_IsAConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, branch, StringComparison.Ordinal);
        using var client = host.Client();
        using var first = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{id}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{id}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(second, ct).ConfigureAwait(true)).ShouldBe("pull_request_exists");

        var another = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var anotherBranch = another.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, anotherBranch, StringComparison.Ordinal);
        host.GitHub.PullRequestAlreadyExists = true;
        using var exists = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{another.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);
        exists.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(exists, ct).ConfigureAwait(true)).ShouldBe("pull_request_exists");
    }

    /// <summary>Bad pull request requests are refused with their own codes before GitHub is asked anything.</summary>
    [Theory]
    [InlineData("{}", "invalid_title")]
    [InlineData("{\"title\":\"   \"}", "invalid_title")]
    [InlineData("{\"title\":\"T\",\"baseBranch\":\"--evil\"}", "invalid_base_branch")]
    [InlineData("{\"title\":\"T\",\"repository\":\"https://github.com/acme/other\"}", "unknown_repository")]
    public async Task InvalidPullRequestRequests_AreRefused(string body, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var before = host.GitHub.Requests.Count;
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", body, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe(code);
        host.GitHub.Requests.Count.ShouldBe(before);
    }

    /// <summary>With no App and no token, an agent still works on what the worker can read, but a pull request cannot be opened and says why.</summary>
    [Fact]
    public async Task NoGitCredential_RefusesThePullRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe("git_not_configured");
        host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull().ContainsKey("AUTONOGRAMMER_GIT_TOKEN").ShouldBeFalse();
        host.GitHub.Requests.ShouldBeEmpty();
    }

    /// <summary>Without an App, a configured personal access token is used for every call and for the worker; no installation token is minted.</summary>
    [Fact]
    public async Task Pat_IsTheFallbackCredential()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Git:Pat"] = PatValue }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, branch, StringComparison.Ordinal);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        host.GitHub.Mints.ShouldBe(0);
        host.GitHub.Requests.All(static request => string.Equals(request.Authorization, "Bearer " + PatValue, StringComparison.Ordinal)).ShouldBeTrue();
        host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull()["AUTONOGRAMMER_GIT_TOKEN"].ShouldBe(PatValue);
    }

    /// <summary>An installation token is reused while it has more than five minutes left and minted again before it expires, never after.</summary>
    [Fact]
    public async Task InstallationToken_IsRefreshedBeforeItExpires()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        using var client = host.Client();
        var baseline = host.GitHub.Mints;

        host.Clock.Advance(TimeSpan.FromMinutes(30));
        using var early = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{id}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        host.GitHub.Mints.ShouldBe(baseline);

        host.Clock.Advance(TimeSpan.FromMinutes(26));
        using var late = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{id}/pull-request", "{\"title\":\"T\"}", ct).ConfigureAwait(true);
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        host.GitHub.Mints.ShouldBeGreaterThan(baseline);
    }

    /// <summary>An installation token is delivered to the worker, but appears in no response, no event of a worker that prints it, and no log line.</summary>
    [Fact]
    public async Task InstallationToken_NeverAppearsInOutput()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Worker.OnPrompt = static _ => ["token: " + FakeGitHubHandler.TokenOf(1) + " end"];
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;
        var token = FakeGitHubHandler.TokenOf(1);

        using var accepted = await host.PromptAsync(id, "print the token", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var session = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        host.Environment.VariablesOf(sessionId).ShouldNotBeNull()["AUTONOGRAMMER_GIT_TOKEN"].ShouldBe(token);
        agent.GetRawText().ShouldNotContain(token, Case.Sensitive);
        string.Concat(session.Events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Select(static payload => payload.Delta)).ShouldBe("token: [REDACTED] end");
        host.Logs.Messages.Any(message => message.Contains(token, StringComparison.Ordinal)).ShouldBeFalse();
        host.Logs.Messages.Any(static message => message.Contains("ghs_", StringComparison.Ordinal)).ShouldBeFalse();
    }

    /// <summary>A pull request title and description are redacted before they are sent to the host.</summary>
    [Fact]
    public async Task PullRequestText_IsRedacted()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Git:Pat"] = PatValue }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var branch = agent.GetProperty("branch").GetString()!;
        host.GitHub.BranchExists = candidate => string.Equals(candidate, branch, StringComparison.Ordinal);
        using var client = host.Client();

        using var response = await CloudApiHost.PostJsonAsync(client, $"/cloud/agents/{agent.GetProperty("id").GetString()}/pull-request", "{\"title\":\"Fix with " + PatValue + "\",\"body\":\"key " + PatValue + "\"}", ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var create = host.GitHub.Requests.Single(static request => string.Equals(request.Method, "POST", StringComparison.Ordinal) && request.Path.EndsWith("/pulls", StringComparison.Ordinal));
        create.Body.ShouldNotContain(PatValue, Case.Sensitive);
        create.Body.ShouldContain("[REDACTED]", Case.Sensitive);
    }

    /// <summary>The repository's own environment file is read through the git host, resolved, and handed to the worker as one variable; it is never executed by the control plane.</summary>
    [Fact]
    public async Task EnvironmentFile_IsResolvedAndPassedToTheWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.Files[".autonogrammer/environment.json"] = "{\"install\":\"npm ci\",\"start\":\"npm start\",\"terminals\":[{\"name\":\"dev\",\"command\":\"npm run dev\"}]}";
        host.GitHub.Files[".cursor/environment.json"] = "{\"install\":\"yarn\"}";

        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        var json = host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull()["AUTONOGRAMMER_ENVIRONMENT"];
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("install").GetString().ShouldBe("npm ci");
        document.RootElement.GetProperty("start").GetString().ShouldBe("npm start");
        document.RootElement.GetProperty("source").GetString().ShouldBe(".autonogrammer/environment.json");
        document.RootElement.GetProperty("terminals")[0].GetProperty("name").GetString().ShouldBe("dev");
    }

    /// <summary>Cursor's file is used when the repository has no Autonogrammer file.</summary>
    [Fact]
    public async Task CursorEnvironmentFile_IsTheFallback()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.Files[".cursor/environment.json"] = "{\"install\":\"yarn\"}";

        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);

        var json = host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull()["AUTONOGRAMMER_ENVIRONMENT"];
        json.ShouldContain(".cursor/environment.json", Case.Sensitive);
    }

    /// <summary>An invalid environment file fails the agent with its validation errors instead of starting it with a guess.</summary>
    [Fact]
    public async Task InvalidEnvironmentFile_FailsTheAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.Files[".autonogrammer/environment.json"] = "{\"install\":42}";

        var created = await host.CreateAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var failed = await host.WaitForStatusAsync(created.GetProperty("id").GetString()!, "failed", cancellationToken: ct).ConfigureAwait(true);

        failed.GetProperty("error").GetProperty("code").GetString().ShouldBe("invalid_environment_config");
        failed.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("'install' must be a string", Case.Sensitive);
    }

    /// <summary>A repository file applies only to a single-repository agent, as in Cursor.</summary>
    [Fact]
    public async Task EnvironmentFile_IsIgnoredForAMultiRepositoryAgent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.Files[".autonogrammer/environment.json"] = "{\"install\":\"npm ci\"}";
        var body = "{\"repositories\":[{\"url\":\"https://github.com/acme/widgets\"},{\"url\":\"https://github.com/acme/gadgets\"}]}";

        var agent = await host.CreateRunningAgentAsync(body, cancellationToken: ct).ConfigureAwait(true);

        var variables = host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull();
        variables.ContainsKey("AUTONOGRAMMER_ENVIRONMENT").ShouldBeFalse();
        variables["AUTONOGRAMMER_REPOSITORIES"].ShouldContain("\"directory\":\"gadgets\"", Case.Sensitive);
    }

    /// <summary>A saved environment definition selected by id beats the repository's file.</summary>
    [Fact]
    public async Task SavedEnvironment_BeatsTheRepositoryFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartWithGitHubAppAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Environments:node:Install"] = "pnpm i", ["Cloud:Environments:node:Start"] = "pnpm dev" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.GitHub.Files[".autonogrammer/environment.json"] = "{\"install\":\"npm ci\"}";

        var agent = await host.CreateRunningAgentAsync(CloudApiHost.DefaultCreateBody("\"environmentId\":\"node\""), cancellationToken: ct).ConfigureAwait(true);

        var json = host.Environment.VariablesOf(agent.GetProperty("sessionId").GetString()!).ShouldNotBeNull()["AUTONOGRAMMER_ENVIRONMENT"];
        json.ShouldContain("pnpm i", Case.Sensitive);
        json.ShouldContain("\"source\":\"personal\"", Case.Sensitive);
        host.GitHub.Requests.Any(static request => request.Path.Contains("/contents/", StringComparison.Ordinal)).ShouldBeFalse();
    }

    private static List<JsonElement> MintBodies(CloudApiHost host) =>
        [.. host.GitHub.Requests
            .Where(static request => string.Equals(request.Method, "POST", StringComparison.Ordinal) && request.Path.EndsWith("/access_tokens", StringComparison.Ordinal))
            .Select(static request =>
            {
                using var document = JsonDocument.Parse(request.Body);
                return document.RootElement.Clone();
            })];

    private static string? Permission(JsonElement mint, string name) =>
        mint.GetProperty("permissions").TryGetProperty(name, out var value) ? value.GetString() : null;
}
