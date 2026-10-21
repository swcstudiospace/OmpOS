using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Behaviour of <see cref="RailwaySwarmProvider"/> against a fake Railway GraphQL endpoint.
/// No test reaches the real Railway API.
/// </summary>
public sealed class RailwaySwarmProviderTests
{
    private const string Token = "test-account-token";
    private const string SandboxId = "svc-123";
    private const string VerifiedEndpoint = "https://backboard.railway.com/graphql/v2";

    [Fact]
    public async Task ProvisionAsync_FailsClosedNamingSandboxCreateWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => throw new InvalidOperationException("provision must not reach the API"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ProvisionAsync(CreateProvisionRequest(), TestContext.Current.CancellationToken));

        ex.Reason.ShouldContain("sandboxCreate");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AttachAsync_FailsClosedWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => throw new InvalidOperationException("attach must not reach the API"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.AttachAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.Reason.ShouldContain("attach");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecAsync_FailsClosedNamingSandboxExecWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => throw new InvalidOperationException("exec must not reach the API"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.ExecAsync(SandboxId, CreateExecRequest()).FirstAsync().AsTask());

        ex.Reason.ShouldContain("sandboxExec");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StreamLogsAsync_FailsClosedNamingDeploymentLogsWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => throw new InvalidOperationException("logs must not reach the API"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.StreamLogsAsync(SandboxId, new LogCursor { Position = 0, Follow = true }).FirstAsync().AsTask());

        ex.Reason.ShouldContain("deploymentLogs");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SnapshotAsync_FailsClosedNamingSandboxCheckpointCreateWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => throw new InvalidOperationException("snapshot must not reach the API"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<NotAvailableException>(
            () => provider.SnapshotAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.Reason.ShouldContain("sandboxCheckpointCreate");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task TeardownAsync_DeletesServiceThroughVerifiedMutationWithBearerToken()
    {
        using var handler = new FakeRailwayHandler(() => Json(HttpStatusCode.OK, """{"data":{"serviceDelete":true}}"""));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        await provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(1);
        var sent = handler.Requests[0];
        sent.Uri.ShouldBe(new Uri(VerifiedEndpoint));
        sent.Authorization.ShouldBe($"Bearer {Token}");
        var body = JsonNode.Parse(sent.Body)!.AsObject();
        body["query"]!.GetValue<string>().ShouldContain("serviceDelete(id: $id)");
        body["variables"]!["id"]!.GetValue<string>().ShouldBe(SandboxId);
    }

    [Fact]
    public async Task TeardownAsync_GraphQlErrorInSuccessfulResponseIsTypedFailure()
    {
        using var handler = new FakeRailwayHandler(() => Json(
            HttpStatusCode.OK,
            """{"errors":[{"message":"Not Authorized","extensions":{"code":"INTERNAL_SERVER_ERROR"}}],"data":null}"""));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.SandboxId.ShouldBe(SandboxId);
        ex.Attempts.ShouldBe(1);
        ex.Message.ShouldContain("Not Authorized");
    }

    [Fact]
    public async Task TeardownAsync_ServerEchoedTokenIsRedactedFromException()
    {
        using var handler = new FakeRailwayHandler(() => Json(
            HttpStatusCode.OK,
            $$"""{"errors":[{"message":"invalid token {{Token}}"}],"data":null}"""));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("invalid token");
        ex.Message.ShouldNotContain(Token);
    }

    [Theory]
    [InlineData(429, """{"errors":[{"message":"Rate limited"}]}""")]
    [InlineData(400, """{"errors":[{"message":"Bad input"}]}""")]
    [InlineData(200, """{"data":{"serviceDelete":false}}""")]
    [InlineData(200, """{"data":{"serviceDelete":null}}""")]
    [InlineData(200, """{"data":null}""")]
    [InlineData(200, "not json")]
    [InlineData(200, "[]")]
    public async Task TeardownAsync_RejectedOrUnconfirmedDeleteIsTypedFailure(int status, string body)
    {
        using var handler = new FakeRailwayHandler(() => Json((HttpStatusCode)status, body));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.SandboxId.ShouldBe(SandboxId);
        ex.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task TeardownAsync_TransportFailureIsWrappedWithInnerException()
    {
        var inner = new HttpRequestException("connection reset");
        using var handler = new FakeRailwayHandler(() => throw inner);
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task TeardownAsync_HttpClientTimeoutIsTypedFailureNotCancellation()
    {
        using var handler = new FakeRailwayHandler(() => throw new TaskCanceledException("request timed out"));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        var ex = await Should.ThrowAsync<TeardownFailedException>(
            () => provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("timed out");
    }

    [Fact]
    public async Task TeardownAsync_CallerCancellationPropagatesUnwrapped()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var handler = new FakeRailwayHandler(() => throw new OperationCanceledException(cts.Token));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        await Should.ThrowAsync<OperationCanceledException>(
            () => provider.TeardownAsync(SandboxId, cts.Token));
    }

    [Fact]
    public async Task TeardownAsync_UnconfiguredProviderCompletesWithoutNetwork()
    {
        var provider = new RailwaySwarmProvider();

        await provider.TeardownAsync(SandboxId, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TeardownAsync_BlankSandboxIdIsRejectedWithoutSendingRequest()
    {
        using var handler = new FakeRailwayHandler(() => Json(HttpStatusCode.OK, """{"data":{"serviceDelete":true}}"""));
        using var client = new HttpClient(handler);
        var provider = new RailwaySwarmProvider(client, Token);

        await Should.ThrowAsync<ArgumentException>(() => provider.TeardownAsync("   "));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_RejectsBlankToken()
    {
        using var handler = new FakeRailwayHandler(() => Json(HttpStatusCode.OK, "{}"));
        using var client = new HttpClient(handler);

        Should.Throw<ArgumentException>(() =>
        {
            _ = new RailwaySwarmProvider(client, "   ");
        });
    }

    [Fact]
    public void Constructor_RejectsNullClient()
    {
        Should.Throw<ArgumentNullException>(() =>
        {
            _ = new RailwaySwarmProvider(null!, Token);
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ProvisionRequest CreateProvisionRequest() => new()
    {
        IdempotencyKey = "railway-test-key",
        SessionId = "railway-test-session",
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

    private sealed record CapturedRequest(Uri? Uri, string? Authorization, string Body);

    private sealed class FakeRailwayHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(request.RequestUri, request.Headers.Authorization?.ToString(), body));
            return respond();
        }
    }
}
