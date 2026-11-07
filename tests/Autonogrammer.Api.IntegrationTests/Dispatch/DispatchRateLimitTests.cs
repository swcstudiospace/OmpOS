using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>The per-caller token buckets: commands burst at ten, reads at one hundred and twenty, and each subject has its own bucket.</summary>
public sealed class DispatchRateLimitTests
{
    /// <summary>The command bucket admits a burst of ten; the eleventh command in the same minute is 429 with a Retry-After.</summary>
    [Fact]
    public async Task The_command_bucket_admits_a_burst_of_ten_then_answers_429()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        var deviceId = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);
        var path = "/dispatch/devices/" + deviceId + "/commands";

        for (var index = 0; index < 10; index++)
        {
            var admitted = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"n\":" + index + "}"), ct).ConfigureAwait(true);
            admitted.Status.ShouldBe(HttpStatusCode.Accepted);
        }

        var limited = await DispatchTestClient.SendAsync(client, HttpMethod.Post, path, DispatchTestClient.CommandJson(payloadJson: "{\"n\":10}"), ct).ConfigureAwait(true);
        limited.Status.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>Two subjects have separate command buckets: one subject's exhausted bucket does not refuse the other.</summary>
    [Fact]
    public async Task Two_subjects_have_separate_command_buckets()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Auth.AllowedSubjects.Add("other");
        using var owner = DispatchTestClient.As(host);
        var ownerDevice = await DispatchTestClient.PairAsync(owner, ct, "Owner desk").ConfigureAwait(true);
        using var other = DispatchTestClient.As(host, "other");
        var otherDevice = await DispatchTestClient.PairAsync(other, ct, "Other desk").ConfigureAwait(true);

        for (var index = 0; index < 10; index++)
        {
            _ = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + ownerDevice + "/commands", DispatchTestClient.CommandJson(payloadJson: "{\"n\":" + index + "}"), ct).ConfigureAwait(true);
        }

        var exhausted = await DispatchTestClient.SendAsync(owner, HttpMethod.Post, "/dispatch/devices/" + ownerDevice + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        exhausted.Status.ShouldBe(HttpStatusCode.TooManyRequests);

        var unaffected = await DispatchTestClient.SendAsync(other, HttpMethod.Post, "/dispatch/devices/" + otherDevice + "/commands", DispatchTestClient.CommandJson(), ct).ConfigureAwait(true);
        unaffected.Status.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>The read bucket admits one hundred and twenty reads a minute; the one hundred and twenty-first is 429.</summary>
    [Fact]
    public async Task The_read_bucket_admits_120_reads_a_minute_then_answers_429()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await DispatchApiHost.StartAsync(ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        using var client = DispatchTestClient.As(host);
        _ = await DispatchTestClient.PairAsync(client, ct).ConfigureAwait(true);

        for (var index = 0; index < 120; index++)
        {
            var read = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
            read.Status.ShouldBe(HttpStatusCode.OK);
        }

        var limited = await DispatchTestClient.SendAsync(client, HttpMethod.Get, "/dispatch/devices", null, ct).ConfigureAwait(true);
        limited.Status.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
