using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>A steer from a Kanban card is a result: the HTTP client's own deadline is a refusal, and a steer the caller cancels is still cancelled.</summary>
public sealed class WebCardSteeringTests
{
    /// <summary>An API that accepts the connection and never answers ends the steer at the client's deadline as a failed result, not an exception.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ASteerThatTimesOutIsAFailedResult()
    {
        using var silent = new HangingHandler();
        using var http = new HttpClient(silent, disposeHandler: false)
        {
            BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute),
            Timeout = TimeSpan.FromMilliseconds(50),
        };
        var steering = new WebCardSteering(new OmpApiClient(http));

        var result = await steering.SteerAsync("s-1", "hello", CardSteerMode.FollowUp, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Outcome.ShouldBe(CardSteerOutcome.Failed);
        result.IsAccepted.ShouldBeFalse();
        result.Message.ShouldBe("The API did not answer in time.");
    }

    /// <summary>A steer the caller cancels is cancelled, not reported as a failed result.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ACancelledSteerIsStillCancelled()
    {
        using var silent = new HangingHandler();
        using var http = new HttpClient(silent, disposeHandler: false) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);
        var steering = new WebCardSteering(new OmpApiClient(http));

        await Should.ThrowAsync<OperationCanceledException>(() => steering.SteerAsync("s-1", "hello", CardSteerMode.FollowUp, cancelled.Token)).ConfigureAwait(true);
    }
}
