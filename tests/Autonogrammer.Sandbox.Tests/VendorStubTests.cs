using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Sandboxes;
using Autonogrammer.Sandbox.Abstractions;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Tests for vendor stub providers - typed failures and capability honesty.
/// </summary>
public sealed class VendorStubTests
{
    [Fact]
    public void OpenShellProvider_Capabilities_AreHonest()
    {
        var runner = new FakeProcessRunner();
        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);

        provider.Capabilities.Isolation.ShouldBe(Isolation.Container);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenShellProvider_AllOperations_ThrowNotAvailable()
    {
        var runner = new FakeProcessRunner();
        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OpenShellProvider_CheckPrerequisites_ReturnsFailureForPodman493()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(new ProcessResult
        {
            ExitCode = 0,
            Stdout = "podman version 4.9.3",
            Stderr = string.Empty,
            TimedOut = false,
        });

        var logger = NullLogger<OpenShellProvider>.Instance;
        var provider = new OpenShellProvider(runner, logger);

        var result = await provider.CheckPrerequisitesAsync(TestContext.Current.CancellationToken);

        result.AllMet.ShouldBeFalse();
        result.FailedPrerequisites.ShouldContain(p => p.Contains("podman", StringComparison.Ordinal) && p.Contains("4.9", StringComparison.Ordinal));
        result.PodmanVersion.ShouldBe("podman version 4.9.3");
    }

    [Fact]
    public void AgentOsProvider_Capabilities_AreHonest()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        provider.Capabilities.Isolation.ShouldBe(Isolation.Process);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task AgentOsProvider_Provision_CreatesVmKeyedByIdempotencyKey()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, CreatedVmJson)));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var request = CreateAgentOsProvisionRequest();

        var result = await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        result.Descriptor.Id.ShouldBe("vm-1");
        result.NewlyProvisioned.ShouldBeTrue();
        result.Descriptor.Labels["autonogrammer.idempotency"].ShouldBe(request.IdempotencyKey);
        handler.Requests.Count.ShouldBe(2);
        var call = handler.Requests[0];
        call.Method.ShouldBe(HttpMethod.Put);
        call.Target.ShouldBe("/actors?namespace=ns-test");
        call.BearerToken.ShouldBe(SecretToken);
        call.Json!["key"]!.GetValue<string>().ShouldBe(request.IdempotencyKey);
        call.Json!["name"]!.GetValue<string>().ShouldBe("agentOS");
        call.Json!["crash_policy"]!.GetValue<string>().ShouldBe("sleep");
    }

    [Fact]
    public async Task AgentOsProvider_Provision_SameKeyReturnsExistingVm()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ExistingVmJson)));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        var result = await provider.ProvisionAsync(CreateAgentOsProvisionRequest(), TestContext.Current.CancellationToken);

        result.Descriptor.Id.ShouldBe("vm-1");
        result.NewlyProvisioned.ShouldBeFalse();
    }

    [Fact]
    public async Task AgentOsProvider_Provision_AppliesEnvironmentThroughConfigPatch()
    {
        using var handler = new ScriptedAgentOsHandler((sent, _) => Task.FromResult(
            sent.Target.StartsWith("/actors", StringComparison.Ordinal)
                ? JsonResponse(HttpStatusCode.OK, CreatedVmJson)
                : JsonResponse(HttpStatusCode.OK, NullOutputJson)));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var baseRequest = CreateAgentOsProvisionRequest();
        var request = baseRequest with
        {
            Spec = baseRequest.Spec with
            {
                Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["DEBUG"] = "1" },
            },
        };

        await provider.ProvisionAsync(request, TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(3);
        var patch = handler.Requests[2];
        patch.Method.ShouldBe(HttpMethod.Post);
        patch.Target.ShouldBe("/gateway/vm-1/action/v1.config.patch");
        patch.RivetToken.ShouldBe(SecretToken);
        patch.Json!["args"]![0]!["patch"]!["environment"]!["DEBUG"]!.GetValue<string>().ShouldBe("1");
    }

    [Fact]
    public async Task AgentOsProvider_Provision_UnmappedIsolation_FailsBeforeAnyHostCall()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var request = CreateAgentOsProvisionRequest() with { Isolation = Isolation.Container };

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentOsProvider_Provision_UnmappedImage_FailsBeforeAnyHostCall()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var baseRequest = CreateAgentOsProvisionRequest();
        var request = baseRequest with { Spec = baseRequest.Spec with { Image = "alpine:latest" } };

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentOsProvider_Provision_RejectedCredentials_ReportsStatus()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, "{}")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        var error = await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(CreateAgentOsProvisionRequest()));

        error.Reason.ShouldContain("HTTP 401");
    }

    [Fact]
    public async Task AgentOsProvider_Provision_HostCallPastDeadline_ThrowsProvisionTimeout()
    {
        using var handler = HangUntilCancelled();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var request = CreateAgentOsProvisionRequest() with { Timeout = TimeSpan.FromMilliseconds(50) };

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AgentOsProvider_Exec_RunsArgvWithCapturedStdioAndMapsEvents()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, ProcessRunJson)));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var request = CreateExecRequest() with { Argv = new[] { "echo", "hello", "world" }, WorkingDirectory = string.Empty };

        var events = await provider.ExecAsync("vm-1", request, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        events.ShouldBe(new List<ExecEvent>
        {
            new() { Type = ExecEventType.Stdout, Stdout = "hello\n" },
            new() { Type = ExecEventType.Stderr, Stderr = "oops\n" },
            new() { Type = ExecEventType.Exit, ExitCode = 3 },
        });
        var call = handler.Requests.ShouldHaveSingleItem();
        call.Target.ShouldBe("/gateway/vm-1/action/v1.process.run");
        call.RivetToken.ShouldBe(SecretToken);
        var input = call.Json!["args"]![0]!;
        input["command"]!.GetValue<string>().ShouldBe("echo");
        input["args"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray().ShouldBe(ExpectedEchoArgs);
        input["options"]!["captureStdio"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void AgentOsProvider_Exec_WorkingDirectory_FailsBeforeAnyHostCall()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        Should.Throw<NotAvailableException>(() => provider.ExecAsync("vm-1", CreateExecRequest()));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentOsProvider_Exec_HostCallPastDeadline_ThrowsTimeout()
    {
        using var handler = HangUntilCancelled();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        var request = CreateExecRequest() with { WorkingDirectory = string.Empty, Timeout = TimeSpan.FromMilliseconds(50) };

        await Should.ThrowAsync<TimeoutException>(() => provider.ExecAsync("vm-1", request).ToListAsync(TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task AgentOsProvider_Attach_ProbesVmStatusWithEmptyArguments()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"output":{}}""")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        await provider.AttachAsync("vm-1", TestContext.Current.CancellationToken);

        var call = handler.Requests.ShouldHaveSingleItem();
        call.Method.ShouldBe(HttpMethod.Post);
        call.Target.ShouldBe("/gateway/vm-1/action/v1.vm.status");
        call.Json!["args"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentOsProvider_Attach_UnknownVm_ThrowsNotAvailable()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{}")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        var error = await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("vm-1", TestContext.Current.CancellationToken));

        error.Reason.ShouldContain("HTTP 404");
    }

    [Fact]
    public async Task AgentOsProvider_Teardown_DestroysActorThroughControlPlane()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        await provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken);

        var call = handler.Requests.ShouldHaveSingleItem();
        call.Method.ShouldBe(HttpMethod.Delete);
        call.Target.ShouldBe("/actors/vm-1?namespace=ns-test");
        call.BearerToken.ShouldBe(SecretToken);
    }

    [Fact]
    public async Task AgentOsProvider_Teardown_AlreadyDestroyedVm_IsIdempotent()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{}")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        await provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AgentOsProvider_Teardown_ServerError_ThrowsTeardownFailed()
    {
        using var handler = new ScriptedAgentOsHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.InternalServerError, "{}")));
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        var error = await Should.ThrowAsync<TeardownFailedException>(() => provider.TeardownAsync("vm-1", TestContext.Current.CancellationToken));

        error.SandboxId.ShouldBe("vm-1");
        error.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task AgentOsProvider_Snapshot_IsNotSupportedWithoutHostCall()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);

        var result = await provider.SnapshotAsync("vm-1", TestContext.Current.CancellationToken);

        result.NotSupported.ShouldNotBeNull();
        result.Data.ShouldBeNull();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void AgentOsProvider_StreamLogs_IsNotAvailable()
    {
        using var handler = NoHostCalls();
        using var http = new HttpClient(handler);
        var provider = CreateAgentOsProvider(http);
        Should.Throw<NotAvailableException>(() => provider.StreamLogsAsync("vm-1"));
    }

    private const string SecretToken = "sk_test_secret";
    private const string CreatedVmJson = """{"actor":{"actor_id":"vm-1"},"created":true}""";
    private const string ExistingVmJson = """{"actor":{"actor_id":"vm-1"},"created":false}""";
    private const string NullOutputJson = """{"output":null}""";
    private const string ProcessRunJson = """{"output":{"status":{"exitCode":3},"stdout":"hello\n","stderr":"oops\n"}}""";
    private static readonly string[] ExpectedEchoArgs = new[] { "hello", "world" };

    private static AgentOsProvider CreateAgentOsProvider(HttpClient httpClient) =>
        new(httpClient, new Uri("https://api.rivet.dev"), "ns-test", SecretToken);

    private static ScriptedAgentOsHandler NoHostCalls() =>
        new((request, _) => throw new InvalidOperationException($"unexpected host call {request.Method} {request.Target}"));

    private static ScriptedAgentOsHandler HangUntilCancelled() =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return JsonResponse(HttpStatusCode.OK, "{}");
        });

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ProvisionRequest CreateAgentOsProvisionRequest() => new()
    {
        IdempotencyKey = "autonogrammer-session-key",
        SessionId = "session-1",
        Timeout = TimeSpan.FromSeconds(30),
        Isolation = Isolation.Process,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = Array.Empty<MountSpec>(),
        SecretRefs = Array.Empty<SecretRef>(),
        Spec = new SandboxSpec
        {
            Image = string.Empty,
            Command = Array.Empty<string>(),
            WorkingDirectory = string.Empty,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

    private sealed class ScriptedAgentOsHandler(Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rivetToken = request.Headers.TryGetValues("x-rivet-token", out var values) ? values.Single() : null;
            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                request.Headers.Authorization?.Parameter,
                rivetToken,
                body);
            Requests.Add(captured);
            return await respond(captured, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Target, string? BearerToken, string? RivetToken, string Body)
    {
        public JsonNode? Json => Body.Length == 0 ? null : JsonNode.Parse(Body);
    }

    [Fact]
    public void RailwaySwarmProvider_Capabilities_AreHonest()
    {
        var provider = new RailwaySwarmProvider();

        provider.Capabilities.Isolation.ShouldBe(Isolation.Container);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task RailwaySwarmProvider_AllOperations_ThrowNotAvailable()
    {
        var provider = new RailwaySwarmProvider();
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AttestedEnclaveProvider_Capabilities_AreHonest()
    {
        var provider = new AttestedEnclaveProvider();

        provider.Capabilities.Isolation.ShouldBe(Isolation.Process);
        provider.Capabilities.Isolation.ShouldNotBe(Isolation.Enclave);
        provider.Capabilities.Attestation.ShouldBe(Attestation.None);
        provider.Capabilities.NativeBinaries.ShouldBeFalse();
        provider.Capabilities.SnapshotsSupported.ShouldBeFalse();
    }

    [Fact]
    public async Task AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified()
    {
        var provider = new AttestedEnclaveProvider();

        var quote = await provider.GetQuoteAsync("any", new byte[] { 1, 2, 3, 4 }, TestContext.Current.CancellationToken);

        quote.Verdict.ShouldBe(AttestationVerdict.Unverified);
        quote.Quote.ShouldNotBeNull();
    }

    [Fact]
    public async Task AttestedEnclaveProvider_AllOperations_ThrowNotAvailable()
    {
        var provider = new AttestedEnclaveProvider();
        var request = CreateProvisionRequest();

        await Should.ThrowAsync<NotAvailableException>(() => provider.ProvisionAsync(request));
        await Should.ThrowAsync<NotAvailableException>(() => provider.AttachAsync("any"));
        await Should.ThrowAsync<NotAvailableException>(() => provider.ExecAsync("any", CreateExecRequest()).FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.StreamLogsAsync("any").FirstAsync().AsTask());
        await Should.ThrowAsync<NotAvailableException>(() => provider.SnapshotAsync("any"));
        await provider.TeardownAsync("any", TestContext.Current.CancellationToken);
    }

    private static ProvisionRequest CreateProvisionRequest() => new()
    {
        IdempotencyKey = $"test-key-{Guid.NewGuid():N}",
        SessionId = $"session-{Guid.NewGuid():N}",
        Timeout = TimeSpan.FromMinutes(5),
        Isolation = Isolation.Container,
        NetworkPolicy = NetworkPolicy.DefaultDeny,
        Mounts = Array.Empty<MountSpec>(),
        SecretRefs = Array.Empty<SecretRef>(),
        Spec = new SandboxSpec
        {
            Image = "alpine:latest",
            Command = new[] { "sleep", "3600" },
            WorkingDirectory = "/workspace",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Limits = null,
        },
    };

    private static ExecRequest CreateExecRequest() => new()
    {
        Argv = new[] { "echo", "hello" },
        WorkingDirectory = "/workspace",
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        Timeout = TimeSpan.FromSeconds(30),
        Tty = false,
    };
}
