using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Autonogrammer.Api.Cloud;
using Autonogrammer.Contracts.Cloud;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>Secrets: the write-only routes, their limits, how a secret reaches a worker (only at provisioning, only as that worker's variables), and the canary proof that no secret value leaves the control plane in a response, an event, a stored transcript or a log line.</summary>
public sealed class CloudSecretTests
{
    private const string Canary = "canary-secret-value-9f3a1c7e";
    private const string SecretName = "DATABASE_PASSWORD";

    /// <summary>A secret can be stored, read back as metadata only, listed and deleted; no response ever holds the value.</summary>
    [Fact]
    public async Task SecretRoutes_AreWriteOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var put = await PutAsync(client, SecretName, Canary, ct).ConfigureAwait(true);
        var putBody = await put.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var get = await client.GetAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        var getBody = await get.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var list = await client.GetAsync(new Uri("/cloud/secrets", UriKind.Relative), ct).ConfigureAwait(true);
        var listBody = await list.Content.ReadAsStringAsync(ct).ConfigureAwait(true);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var body in new[] { putBody, getBody, listBody })
        {
            body.ShouldNotContain(Canary, Case.Sensitive);
            body.ShouldContain(SecretName, Case.Sensitive);
        }

        using var delete = await client.DeleteAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var gone = await client.GetAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var deleteAgain = await client.DeleteAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        deleteAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A secret belongs to its owner: another owner sees neither its existence nor its value.</summary>
    [Fact]
    public async Task Secrets_AreScopedToTheirOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var alice = host.Client();
        using var bob = host.Client(CloudApiHost.Bob);
        using var put = await PutAsync(alice, SecretName, Canary, ct).ConfigureAwait(true);
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var get = await bob.GetAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        using var list = await bob.GetAsync(new Uri("/cloud/secrets", UriKind.Relative), ct).ConfigureAwait(true);
        using var agent = await CloudApiHost.PostJsonAsync(bob, "/cloud/agents", CloudApiHost.DefaultCreateBody("\"secretRefs\":[\"" + SecretName + "\"]"), ct).ConfigureAwait(true);

        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await list.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldNotContain(SecretName, Case.Sensitive);
        agent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CloudApiHost.ProblemCodeAsync(agent, ct).ConfigureAwait(true)).ShouldBe("unknown_secret");
    }

    /// <summary>Names that would override the worker's own settings or the loader, and values that cannot be redacted reliably, are refused.</summary>
    [Theory]
    [InlineData("AUTONOGRAMMER_AGENT_ID", "long-enough-value", "invalid_secret_name")]
    [InlineData("Worker__SessionId", "long-enough-value", "invalid_secret_name")]
    [InlineData("RAILWAY_TOKEN", "long-enough-value", "invalid_secret_name")]
    [InlineData("LD_PRELOAD", "long-enough-value", "invalid_secret_name")]
    [InlineData("PATH", "long-enough-value", "invalid_secret_name")]
    [InlineData("GIT_ASKPASS", "long-enough-value", "invalid_secret_name")]
    [InlineData("1BAD", "long-enough-value", "invalid_secret_name")]
    [InlineData("has-dash", "long-enough-value", "invalid_secret_name")]
    [InlineData("OK_NAME", "abc", "invalid_secret_value")]
    [InlineData("OK_NAME", "bad\u0000value", "invalid_secret_value")]
    public async Task InvalidSecrets_AreRefused(string name, string value, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();

        using var response = await PutAsync(client, name, value, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CloudApiHost.ProblemCodeAsync(response, ct).ConfigureAwait(true)).ShouldBe(code);
    }

    /// <summary>An owner at the secret cap cannot add a new name, but can still replace an existing one.</summary>
    [Fact]
    public async Task SecretCap_RefusesNewNamesOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Limits:MaxSecretsPerOwner"] = "1" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();
        using var first = await PutAsync(client, "FIRST_SECRET", "first-secret-value", ct).ConfigureAwait(true);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await PutAsync(client, "SECOND_SECRET", "second-secret-value", ct).ConfigureAwait(true);
        using var replace = await PutAsync(client, "FIRST_SECRET", "replaced-secret-value", ct).ConfigureAwait(true);

        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await CloudApiHost.ProblemCodeAsync(second, ct).ConfigureAwait(true)).ShouldBe("secret_limit_reached");
        replace.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A secret reaches the worker as an environment variable of that worker's own service at provisioning, together with the repository list, the agent's branch and the session pin.</summary>
    [Fact]
    public async Task Provisioning_DeliversSecretsAndCheckoutInstructionsAsWorkerVariables()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using (var client = host.Client())
        {
            using var put = await PutAsync(client, SecretName, Canary, ct).ConfigureAwait(true);
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var agent = await host.CreateRunningAgentAsync(CloudApiHost.DefaultCreateBody("\"secretRefs\":[\"" + SecretName + "\"]"), cancellationToken: ct).ConfigureAwait(true);

        var sessionId = agent.GetProperty("sessionId").GetString()!;
        var variables = host.Environment.VariablesOf(sessionId).ShouldNotBeNull();
        variables[SecretName].ShouldBe(Canary);
        variables["Worker__SessionId"].ShouldBe(sessionId);
        variables["AUTONOGRAMMER_AGENT_ID"].ShouldBe(agent.GetProperty("id").GetString());
        variables["AUTONOGRAMMER_AGENT_BRANCH"].ShouldBe(agent.GetProperty("branch").GetString());
        variables["AUTONOGRAMMER_REPOSITORIES"].ShouldContain("\"directory\":\"widgets\"", Case.Sensitive);
        variables["AUTONOGRAMMER_REPOSITORIES"].ShouldContain(CloudApiHost.RepoUrl, Case.Sensitive);
        variables["AUTONOGRAMMER_SECRET_NAMES"].ShouldBe("[\"" + SecretName + "\"]");
        variables.ContainsKey("AUTONOGRAMMER_GIT_TOKEN").ShouldBeFalse();
    }

    /// <summary>A secret deleted between creation and provisioning fails the agent with a clear code instead of starting a worker without it.</summary>
    [Fact]
    public async Task DeletedSecret_FailsProvisioning()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = host.Client();
        using var put = await PutAsync(client, SecretName, Canary, ct).ConfigureAwait(true);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Environment.CreateGate = gate;
        var created = await host.CreateAgentAsync(CloudApiHost.DefaultCreateBody("\"secretRefs\":[\"" + SecretName + "\"]"), cancellationToken: ct).ConfigureAwait(true);
        using var delete = await client.DeleteAsync(new Uri($"/cloud/secrets/{SecretName}", UriKind.Relative), ct).ConfigureAwait(true);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        gate.SetResult();
        var failed = await host.WaitForStatusAsync(created.GetProperty("id").GetString()!, "failed", cancellationToken: ct).ConfigureAwait(true);

        failed.GetProperty("error").GetProperty("code").GetString().ShouldBe("secret_missing");
        failed.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNull().ShouldContain(SecretName, Case.Sensitive);
        failed.GetRawText().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>
    /// The canary test. A worker that echoes the secret (whole, split across two stream chunks, inside a tool call, and in the first words of an answer)
    /// leaves no trace of it: not in the stored transcript, not on the event stream, not in a response, not in a log line. The prompt that quoted it is
    /// stored redacted too.
    /// </summary>
    [Fact]
    public async Task CanarySecret_NeverAppearsInAnyOutput()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using (var client = host.Client())
        {
            using var put = await PutAsync(client, SecretName, Canary, ct).ConfigureAwait(true);
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        host.Worker.OnPrompt = static _ => ["the password is " + Canary + ".", " split: " + Canary[..(Canary.Length / 2)], Canary[(Canary.Length / 2)..] + " done"];
        host.Worker.OnPromptUpdates = static _ =>
        [
            "{\"sessionUpdate\":\"tool_call\",\"toolCallId\":\"t1\",\"title\":\"run\",\"kind\":\"execute\",\"status\":\"in_progress\",\"rawInput\":{\"command\":\"echo " + Canary + "\"}}",
        ];
        var agent = await host.CreateRunningAgentAsync(CloudApiHost.DefaultCreateBody("\"secretRefs\":[\"" + SecretName + "\"]"), cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        var sessionId = agent.GetProperty("sessionId").GetString()!;

        using var accepted = await host.PromptAsync(id, "please use " + Canary + " to connect", cancellationToken: ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var acceptedBody = await accepted.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        var session = await host.WaitForSessionAsync(sessionId, SessionStatus.Completed, ct).ConfigureAwait(true);

        acceptedBody.ShouldNotContain(Canary, Case.Sensitive);
        var transcript = string.Join('\n', session.Messages.Select(static message => message.Content)) + string.Join('\n', session.Events.Select(static evt => evt.Payload.ToString()));
        transcript.ShouldNotContain(Canary, Case.Sensitive);
        session.Messages.Any(static message => message.Content.Contains(CloudSecretRedactor.Marker, StringComparison.Ordinal)).ShouldBeTrue();
        var text = string.Concat(session.Events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Select(static payload => payload.Delta));
        text.ShouldContain("the password is " + CloudSecretRedactor.Marker, Case.Sensitive);
        text.ShouldContain(" split: " + CloudSecretRedactor.Marker + " done", Case.Sensitive);
        session.Events.Any(static evt => evt.Payload is not TextDeltaPayload and not CompletedPayload and not UserMessagePayload).ShouldBeTrue();

        using var stream = host.Client();
        var items = await SessionClient.ReadStreamAsync(stream, $"/cloud/agents/{id}/events", session.Events.Count, ct).ConfigureAwait(true);
        string.Join('\n', items.Select(static item => item.Data)).ShouldNotContain(Canary, Case.Sensitive);
        using var read = await stream.GetAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        (await read.Content.ReadAsStringAsync(ct).ConfigureAwait(true)).ShouldNotContain(Canary, Case.Sensitive);
        host.Logs.Messages.Any(static message => message.Contains(Canary, StringComparison.Ordinal)).ShouldBeFalse();
        var record = await host.Services.GetRequiredService<ICloudAgentStore>().GetAsync(id, ct).ConfigureAwait(true);
        record.ShouldNotBeNull().ToString().ShouldNotContain(Canary, Case.Sensitive);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string name, string value, CancellationToken cancellationToken)
    {
        using var content = new StringContent(new System.Text.Json.Nodes.JsonObject { ["value"] = value }.ToJsonString(), Encoding.UTF8, "application/json");
        return await client.PutAsync(new Uri($"/cloud/secrets/{name}", UriKind.Relative), content, cancellationToken).ConfigureAwait(false);
    }
}
