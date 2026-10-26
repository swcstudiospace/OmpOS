using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sessions;
using OmpOS.Coordination;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Verifies a new turn is admitted only while its session's run lease is free. A lease another owner holds refuses the prompt
/// with a 409 before anything is appended; once that lease is released the same prompt is admitted and runs to completion.
/// </summary>
public sealed class PromptRunLeaseTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a prompt while another owner holds the session's run lease returns 409 and appends no events.</summary>
    [Fact]
    public async Task HeldLease_Returns409AndAppendsNoEvents()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var before = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);

        var leases = factory.Services.GetRequiredService<ISessionRunLeases>();
        await using var held = (await leases.TryAcquireAsync(id, "other-owner", CoordinationDefaults.RunLeaseTimeToLive, testToken).ConfigureAwait(true))
            .ShouldNotBeNull("the test holds the lease before it sends the prompt").ConfigureAwait(true);

        using var response = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var after = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        after.Events.Count.ShouldBe(before.Events.Count, "a refused prompt appends no events");
        after.Messages.ShouldBeEmpty("a refused prompt is never recorded");
        after.Status.ShouldBe(SessionStatus.Idle);
    }

    /// <summary>Verifies a prompt is admitted and runs to completion once the lease another owner held is released.</summary>
    [Fact]
    public async Task ReleasedLease_AdmitsPromptAndRunsToCompletion()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        var leases = factory.Services.GetRequiredService<ISessionRunLeases>();
        var held = (await leases.TryAcquireAsync(id, "other-owner", CoordinationDefaults.RunLeaseTimeToLive, testToken).ConfigureAwait(true))
            .ShouldNotBeNull("the test holds the lease before it releases it");
        await held.DisposeAsync().ConfigureAwait(true);
        (await leases.IsHeldAsync(id, testToken).ConfigureAwait(true)).ShouldBeFalse("the released lease is no longer held");

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
        settled.Messages.Count.ShouldBe(1, "the admitted prompt is recorded once");
    }

    /// <summary>Creates a host whose harness completes every prompt with one scripted delta.</summary>
    /// <returns>The test host factory.</returns>
    private static OmpApiFactory CreateFactory() =>
        new(configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ScriptedHarnessFactory.DeltaScript(1))));
}
