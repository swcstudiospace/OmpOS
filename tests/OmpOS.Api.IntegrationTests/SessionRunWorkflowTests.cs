using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Harness;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Session-run workflow (WFLW-01): each prompt is one run through <c>DirectSessionRunner</c> —
/// exactly one provision and one teardown per session, with teardown in a finally even when the
/// prompt run fails. The counting decorator replaces the sandbox registration through the same
/// test factory pattern the harness replacements use.
/// </summary>
public sealed class SessionRunWorkflowTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies one prompt produces exactly one provision and one teardown for its session.</summary>
    [Fact]
    public async Task Prompt_CompletesWithExactlyOneProvisionAndTeardown()
    {
        var counting = new CountingSandboxProvider(new FakeSandboxProvider());
        var script = ScriptedHarnessFactory.DeltaScript(1);
        using var factory = new OmpApiFactory(configureServices: services =>
        {
            services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script));
            services.AddSingleton<ISandboxProvider>(counting);
        });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken);
        await WaitForCountAsync(() => counting.TeardownsFor(id), 1, testToken);

        counting.ProvisionsFor(id).ShouldBe(1, "one prompt is one run with one provision");
        counting.TeardownsFor(id).ShouldBe(1, "the run tears down exactly once");
    }

    /// <summary>
    /// Verifies the teardown still happens when the prompt run fails: a scripted mid-stream harness
    /// failure settles the session as failed (with an error event) and the sandbox is torn down.
    /// </summary>
    [Fact]
    public async Task Prompt_Failure_StillTearsDown()
    {
        var counting = new CountingSandboxProvider(new FakeSandboxProvider());
        var script = new InMemoryScript { Steps = [new FailStep(new MalformedFrameHarnessException("boom"))] };
        using var factory = new OmpApiFactory(configureServices: services =>
        {
            services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script));
            services.AddSingleton<ISandboxProvider>(counting);
        });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken);
        settled.Events.ShouldContain(static evt => evt.Payload is ErrorPayload, "the failed run appends an error event");
        await WaitForCountAsync(() => counting.TeardownsFor(id), 1, testToken);

        counting.ProvisionsFor(id).ShouldBe(1, "the failed run still provisioned exactly once");
        counting.TeardownsFor(id).ShouldBe(1, "teardown runs in a finally even when the prompt fails");
    }

    /// <summary>
    /// Verifies a provision failure settles the session as failed: the workflow reports the failure without
    /// throwing, so the runner must settle the session that the endpoint left running.
    /// </summary>
    [Fact]
    public async Task ProvisionFailure_SettlesSessionAsFailed()
    {
        var sandbox = new FakeSandboxProvider();
        sandbox.FailProvision(1);
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<ISandboxProvider>(sandbox));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken);
        settled.Events.ShouldContain(static evt => evt.Payload is ErrorPayload, "the failed run appends an error event");
    }

    /// <summary>Polls a count until it reaches the expected value or the test times out.</summary>
    /// <param name="read">The count reader.</param>
    /// <param name="expected">The expected count.</param>
    /// <param name="testToken">The test cancellation token.</param>
    private static async Task WaitForCountAsync(Func<int> read, int expected, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        while (read() != expected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), linked.Token).ConfigureAwait(false);
        }
    }
}
