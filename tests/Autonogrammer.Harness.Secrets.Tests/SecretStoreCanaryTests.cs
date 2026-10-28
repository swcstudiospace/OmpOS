using System.Globalization;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>
/// Canary tests through the store: a registered secret is stored and read back, then every rendering a caller can reach
/// without calling <see cref="SecretString.Reveal"/> is written to the four sinks. The scan must stay clean, while a
/// deliberate <see cref="SecretString.Reveal"/> write is detected, which proves the scan can see the canary.
/// </summary>
public sealed class SecretStoreCanaryTests
{
    private const string CanaryValue = "store-canary-secret-7be41d90a3c6";
    private const string CanaryName = "provider.canary";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenderingsOfAStoredSecretNeverReachAnySink()
    {
        InMemorySessionSecretStore store = new();
        CanaryHarness harness = new();
        var secret = new SecretString(CanaryValue);
        harness.Register(secret);
        await store.SetAsync(CanaryName, secret, Token).ConfigureAwait(true);
        SecretString read = (await store.GetAsync(CanaryName, Token).ConfigureAwait(true))!;
        InMemorySecretSink[] sinks = AllChannelSinks();

        foreach (InMemorySecretSink sink in sinks)
        {
            await sink.WriteAsync(sink.Channel, read.ToString(), Token).ConfigureAwait(true);
            await sink.WriteAsync(sink.Channel, $"key for {CanaryName} is {read}", Token).ConfigureAwait(true);
            await sink.WriteAsync(sink.Channel, string.Format(CultureInfo.InvariantCulture, "{0} {0:G}", read), Token).ConfigureAwait(true);
            await sink.WriteAsync(sink.Channel, store.ToString(), Token).ConfigureAwait(true);
            await sink.WriteAsync(sink.Channel, string.Join(',', await store.ListNamesAsync(Token).ConfigureAwait(true)), Token)
                .ConfigureAwait(true);
        }

        harness.Inspect(sinks).ShouldBeEmpty();
        sinks.Single(sink => sink.Channel == SecretSinkChannel.Log).Writes[0].ShouldBe(SecretString.RedactionMarker);
    }

    [Fact]
    public async Task ExceptionMessagesFromStoreRefusalsNeverCarryTheSecret()
    {
        InMemorySessionSecretStore store = new();
        CanaryHarness harness = new();
        var secret = new SecretString(CanaryValue);
        harness.Register(secret);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync().ConfigureAwait(true);
        List<Exception> faults =
        [
            await Should.ThrowAsync<ArgumentException>(store.SetAsync(" ", secret, Token)).ConfigureAwait(true),
            await Should.ThrowAsync<ArgumentNullException>(store.SetAsync(null!, secret, Token)).ConfigureAwait(true),
            await Should.ThrowAsync<OperationCanceledException>(store.SetAsync(CanaryName, secret, cancelled.Token)).ConfigureAwait(true),
        ];
        InMemorySecretSink crashReport = new(SecretSinkChannel.CrashReport);

        foreach (Exception fault in faults)
        {
            await crashReport.WriteAsync(SecretSinkChannel.CrashReport, fault.Message, Token).ConfigureAwait(true);
            await crashReport.WriteAsync(SecretSinkChannel.CrashReport, fault.ToString(), Token).ConfigureAwait(true);
        }

        harness.Inspect([crashReport]).ShouldBeEmpty();
        (await store.ListNamesAsync(Token).ConfigureAwait(true)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SecretRevealedIntoASinkIsDetectedAfterARoundTripThroughTheStore()
    {
        InMemorySessionSecretStore store = new();
        CanaryHarness harness = new();
        var secret = new SecretString(CanaryValue);
        harness.Register(secret);
        await store.SetAsync(CanaryName, secret, Token).ConfigureAwait(true);
        SecretString read = (await store.GetAsync(CanaryName, Token).ConfigureAwait(true))!;
        InMemorySecretSink[] sinks = AllChannelSinks();
        InMemorySecretSink telemetry = sinks.Single(sink => sink.Channel == SecretSinkChannel.Telemetry);

        await telemetry.WriteAsync(SecretSinkChannel.Telemetry, $"upstream key {read.Reveal()}", Token).ConfigureAwait(true);

        harness.Inspect(sinks).Select(leak => leak.Channel).ShouldBe([SecretSinkChannel.Telemetry]);
    }

    private static InMemorySecretSink[] AllChannelSinks() =>
    [
        new InMemorySecretSink(SecretSinkChannel.Log),
        new InMemorySecretSink(SecretSinkChannel.Transcript),
        new InMemorySecretSink(SecretSinkChannel.CrashReport),
        new InMemorySecretSink(SecretSinkChannel.Telemetry),
    ];
}
