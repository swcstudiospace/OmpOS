using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>
/// Verifies that the canary harness reports a registered secret on the channel it reaches and passes writes that
/// carry only the redaction marker.
/// </summary>
public sealed class CanaryHarnessTests
{
    private const string CanaryValue = "canary-fixture-secret-5d2e8a17c0f4";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(SecretSinkChannel.Log)]
    [InlineData(SecretSinkChannel.Transcript)]
    [InlineData(SecretSinkChannel.CrashReport)]
    [InlineData(SecretSinkChannel.Telemetry)]
    public async Task RegisteredCanaryWrittenToAChannelIsDetectedOnThatChannel(SecretSinkChannel channel)
    {
        CanaryHarness harness = new();
        harness.Register(new SecretString(CanaryValue));
        InMemorySecretSink[] sinks = AllChannelSinks();
        InMemorySecretSink target = sinks.Single(sink => sink.Channel == channel);

        await target.WriteAsync(channel, $"request failed with key {CanaryValue}", Token);

        harness.Inspect(sinks).Select(leak => leak.Channel).ShouldBe([channel]);
    }

    [Fact]
    public async Task CanaryWrittenToEachOfTheFourSinksIsDetectedOnEachChannel()
    {
        CanaryHarness harness = new();
        harness.Register(new SecretString(CanaryValue));
        InMemorySecretSink[] sinks = AllChannelSinks();

        foreach (InMemorySecretSink sink in sinks)
        {
            await sink.WriteAsync(sink.Channel, CanaryValue, Token);
        }

        harness.Inspect(sinks).Select(leak => leak.Channel).ShouldBe(
        [
            SecretSinkChannel.Log,
            SecretSinkChannel.Transcript,
            SecretSinkChannel.CrashReport,
            SecretSinkChannel.Telemetry,
        ]);
    }

    [Fact]
    public async Task WritesCarryingOnlyTheRedactionMarkerPassTheScan()
    {
        CanaryHarness harness = new();
        var secret = new SecretString(CanaryValue);
        harness.Register(secret);
        InMemorySecretSink[] sinks = AllChannelSinks();

        foreach (InMemorySecretSink sink in sinks)
        {
            await sink.WriteAsync(sink.Channel, $"provider key {secret}", Token);
            await sink.WriteAsync(sink.Channel, "turn completed", Token);
        }

        harness.Inspect(sinks).ShouldBeEmpty();
    }

    [Fact]
    public async Task CanarySplitAcrossTwoWritesOnOneChannelIsDetected()
    {
        CanaryHarness harness = new();
        harness.Register(new SecretString(CanaryValue));
        InMemorySecretSink sink = new(SecretSinkChannel.Log);
        InMemorySecretSink[] sinks = [sink];
        int split = CanaryValue.Length / 2;

        await sink.WriteAsync(SecretSinkChannel.Log, CanaryValue[..split], Token);
        await sink.WriteAsync(SecretSinkChannel.Log, CanaryValue[split..], Token);

        harness.Inspect(sinks).Select(leak => leak.Channel).ShouldBe([sink.Channel]);
    }

    [Fact]
    public async Task SinkRefusesAWriteAddressedToAnotherChannel()
    {
        var sink = new InMemorySecretSink(SecretSinkChannel.Log);

        await Should.ThrowAsync<InvalidOperationException>(() => sink.WriteAsync(SecretSinkChannel.Telemetry, "anything", Token));
    }

    [Fact]
    public async Task NullTextIsReportedThroughTheReturnedTask()
    {
        var sink = new InMemorySecretSink(SecretSinkChannel.Log);

        await Should.ThrowAsync<ArgumentNullException>(() => sink.WriteAsync(SecretSinkChannel.Log, null!, Token));
    }

    [Fact]
    public async Task CancelledWriteIsReportedThroughTheReturnedTask()
    {
        var sink = new InMemorySecretSink(SecretSinkChannel.Log);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => sink.WriteAsync(SecretSinkChannel.Log, "anything", cancellation.Token));
    }

    private static InMemorySecretSink[] AllChannelSinks() =>
    [
        new InMemorySecretSink(SecretSinkChannel.Log),
        new InMemorySecretSink(SecretSinkChannel.Transcript),
        new InMemorySecretSink(SecretSinkChannel.CrashReport),
        new InMemorySecretSink(SecretSinkChannel.Telemetry),
    ];
}
