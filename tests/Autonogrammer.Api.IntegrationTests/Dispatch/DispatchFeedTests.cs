using System.Net;
using System.Text;
using Autonogrammer.Contracts.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The account feed: resume by Last-Event-ID or ?since=, the header wins, a bad position is 400, the feed is account-scoped, it outlives the request timeout, and a pruned position gets one resync then the tail.</summary>
public sealed class DispatchFeedTests
{
    private const string Kind = "device.presence";

    /// <summary>Resuming by Last-Event-ID starts after that identifier.</summary>
    [Fact]
    public async Task Resuming_by_Last_Event_ID_starts_after_that_identifier()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        await AppendAsync(host, DispatchTestClient.Owner, 3, ct).ConfigureAwait(true);

        var frames = await ReadFramesAsync(client, "/dispatch/events", "1", 2, TimeSpan.FromSeconds(5), ct).ConfigureAwait(true);

        string.Join(",", frames.Select(frame => frame.Id ?? string.Empty)).ShouldBe("2,3");
    }

    /// <summary>Last-Event-ID wins over ?since= when both are sent.</summary>
    [Fact]
    public async Task Last_Event_ID_wins_over_since()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        await AppendAsync(host, DispatchTestClient.Owner, 3, ct).ConfigureAwait(true);

        var frames = await ReadFramesAsync(client, "/dispatch/events?since=0", "2", 1, TimeSpan.FromSeconds(5), ct).ConfigureAwait(true);

        string.Join(",", frames.Select(frame => frame.Id ?? string.Empty)).ShouldBe("3");
    }

    /// <summary>A resume position that is not a non-negative integer is 400 invalid_resume_position.</summary>
    [Fact]
    public async Task A_resume_position_that_is_not_a_non_negative_integer_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);

        var negative = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/events?since=-1", null, ct).ConfigureAwait(true);
        negative.Status.ShouldBe(HttpStatusCode.BadRequest);
        negative.ProblemType().ShouldBe("/problems/invalid_resume_position");

        var notANumber = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/events?since=abc", null, ct).ConfigureAwait(true);
        notANumber.Status.ShouldBe(HttpStatusCode.BadRequest);
        notANumber.ProblemType().ShouldBe("/problems/invalid_resume_position");
    }

    /// <summary>The feed is account-scoped: another subject's stream never carries the owner's events.</summary>
    [Fact]
    public async Task The_feed_never_crosses_accounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Auth.AllowedSubjects.Add("other");
        using var owner = DispatchTestClient.As(host);
        using var other = DispatchTestClient.As(host, "other");
        await AppendAsync(host, DispatchTestClient.Owner, 2, ct).ConfigureAwait(true);

        var ownerFrames = await ReadFramesAsync(owner, "/dispatch/events", null, 2, TimeSpan.FromSeconds(5), ct).ConfigureAwait(true);
        var otherFrames = await ReadFramesAsync(other, "/dispatch/events", null, 2, TimeSpan.FromSeconds(1.5), ct).ConfigureAwait(true);

        ownerFrames.Count.ShouldBe(2);
        otherFrames.Count.ShouldBe(0);
    }

    /// <summary>The stream keeps delivering after a shortened default request timeout, because the feed route is exempt from it.</summary>
    [Fact]
    public async Task The_feed_outlives_a_shortened_default_request_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, configure: static settings => settings["Hosting:RequestTimeoutSeconds"] = "1").ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var appender = Task.Run(
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2.5), ct).ConfigureAwait(false);
                await AppendAsync(host, DispatchTestClient.Owner, 1, ct).ConfigureAwait(false);
            },
            ct);

        var frames = await ReadFramesAsync(client, "/dispatch/events", null, 1, TimeSpan.FromSeconds(8), ct).ConfigureAwait(true);
        await appender.ConfigureAwait(true);

        frames.Count.ShouldBe(1);
    }

    /// <summary>A position older than the retained history yields exactly one resync, then the live tail from the newest retained identifier.</summary>
    [Fact]
    public async Task A_pruned_position_yields_one_resync_then_the_live_tail()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct, configure: static settings => settings["Dispatch:Limits:FeedRetention"] = "00:00:01").ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        await AppendAsync(host, DispatchTestClient.Owner, 1, ct).ConfigureAwait(true);
        await Task.Delay(TimeSpan.FromMilliseconds(1500), ct).ConfigureAwait(true);
        await AppendAsync(host, DispatchTestClient.Owner, 1, ct).ConfigureAwait(true);
        var appender = Task.Run(
            async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct).ConfigureAwait(false);
                await AppendAsync(host, DispatchTestClient.Owner, 1, ct).ConfigureAwait(false);
            },
            ct);

        var frames = await ReadFramesAsync(client, "/dispatch/events", "0", 2, TimeSpan.FromSeconds(8), ct).ConfigureAwait(true);
        await appender.ConfigureAwait(true);

        frames.Count.ShouldBe(2);
        frames[0].Event.ShouldBe("resync");
        frames[1].Id.ShouldBe("3");
    }

    private static async Task AppendAsync(DispatchApiHost host, string accountId, int count, CancellationToken cancellationToken)
    {
        var store = host.Services.GetRequiredService<IDispatchStore>();
        for (var index = 0; index < count; index++)
        {
            _ = await store.AppendFeedEventAsync(accountId, Kind, null, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>One server-sent event as read off the wire.</summary>
    /// <param name="Id">The id field, when sent.</param>
    /// <param name="Event">The event field, when sent.</param>
    /// <param name="Data">The data field.</param>
    private sealed record SseFrame(string? Id, string? Event, string Data);

    private static async Task<IReadOnlyList<SseFrame>> ReadFramesAsync(HttpClient client, string path, string? lastEventId, int expected, TimeSpan budget, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (lastEventId is not null)
        {
            request.Headers.Add("Last-Event-ID", lastEventId);
        }

        using var budgetSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetSource.CancelAfter(budget);
        var frames = new List<SseFrame>();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budgetSource.Token).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var stream = await response.Content.ReadAsStreamAsync(budgetSource.Token).ConfigureAwait(true);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await ParseFramesAsync(reader, frames, expected, budgetSource.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (budgetSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The budget elapsed; the frames read so far are the answer.
        }
        catch (IOException) when (budgetSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The budget elapsed and the test host reports the aborted request as an I/O failure; the frames read so far are the answer.
        }

        return frames;
    }

    private static async Task ParseFramesAsync(StreamReader reader, List<SseFrame> frames, int expected, CancellationToken cancellationToken)
    {
        string? id = null;
        string? type = null;
        var data = new StringBuilder();
        while (frames.Count < expected)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(true);
            if (line is null)
            {
                return;
            }

            if (line.Length == 0)
            {
                if (id is not null || type is not null || data.Length > 0)
                {
                    frames.Add(new SseFrame(id, type, data.ToString()));
                }

                id = null;
                type = null;
                data.Clear();
            }
            else if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                id = line[3..].Trim();
            }
            else if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                type = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line[5..].Trim());
            }
        }
    }
}
