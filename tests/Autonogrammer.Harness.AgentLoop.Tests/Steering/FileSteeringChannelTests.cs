using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Autonogrammer.Harness.AgentLoop.Steering;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Steering;

/// <summary>
/// Verifies the file steering channel against a fake of the omp plugin: the directive file and its atomic write, the three
/// acknowledgement outcomes, the timeout, every refusal (stale heartbeat, missing directory, full inbox, invalid directive),
/// ordering, cancellation, and that no text of a directive ever appears in a message or a description.
/// </summary>
public sealed partial class FileSteeringChannelTests
{
    private const string Secret = "launch-code-7731 steer the refactor";

    private static readonly TimeSpan Dead = TimeSpan.FromSeconds(30);

    [GeneratedRegex(@"\A\d{6}-[0-9a-f]{10}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex IdPattern();

    private static SteeringDirective Directive(string text = Secret, SteeringDirectiveMode mode = SteeringDirectiveMode.Steer) =>
        new() { Mode = mode, Text = text };

    [Fact]
    public async Task SendingWritesOneDirectiveFileWithTheDocumentedSchemaAndNoTemporaryFileLeftBehind()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();

        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        IdPattern().IsMatch(receipt.Id).ShouldBeTrue();
        receipt.Mode.ShouldBe(SteeringDirectiveMode.Steer);
        receipt.QueuedAt.ShouldBe(rig.Clock.GetUtcNow());
        rig.InboxNames().ShouldBe([receipt.Id + ".json"]);
        using var document = rig.ReadDirective(receipt.Id);
        var root = document.RootElement;
        root.GetProperty("v").GetInt32().ShouldBe(1);
        root.GetProperty("id").GetString().ShouldBe(receipt.Id);
        root.GetProperty("mode").GetString().ShouldBe("steer");
        root.GetProperty("text").GetString().ShouldBe(Secret);
        root.GetProperty("createdAt").GetString().ShouldBe("2026-10-26T12:00:00.000Z");
        root.GetProperty("source").GetString().ShouldBe("autonogrammer");
        root.EnumerateObject().Count().ShouldBe(6);
    }

    [Theory]
    [InlineData(SteeringDirectiveMode.Steer, "steer")]
    [InlineData(SteeringDirectiveMode.FollowUp, "followUp")]
    [InlineData(SteeringDirectiveMode.Aside, "aside")]
    public async Task EachModeIsWrittenWithItsWireName(SteeringDirectiveMode mode, string wire)
    {
        using var rig = new SteerRig();
        rig.StartPlugin();

        var receipt = await rig.Channel().SendAsync(Directive(mode: mode), TestContext.Current.CancellationToken).ConfigureAwait(true);

        using var document = rig.ReadDirective(receipt.Id);
        document.RootElement.GetProperty("mode").GetString().ShouldBe(wire);
        receipt.Mode.ShouldBe(mode);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task TheDirectiveFileIsOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes do not exist on Windows.");
        }

        using var rig = new SteerRig();
        rig.StartPlugin();

        var receipt = await rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        File.GetUnixFileMode(Path.Combine(rig.Inbox, receipt.Id + ".json")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task ADeliveredDirectiveIsAcknowledgedWithItsModeAndIdleFlag()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(mode: SteeringDirectiveMode.Aside), TestContext.Current.CancellationToken).ConfigureAwait(true);

        rig.Respond().ShouldBe([receipt.Id]);
        var ack = await channel.WaitForAckAsync(receipt.Id, Dead, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var delivered = ack.ShouldNotBeNull();
        delivered.Id.ShouldBe(receipt.Id);
        delivered.Status.ShouldBe(SteeringAckStatus.Delivered);
        delivered.Reason.ShouldBeNull();
        delivered.Mode.ShouldBe(SteeringDirectiveMode.Aside);
        delivered.Idle.ShouldBe(true);
        delivered.At.ShouldBe(new DateTimeOffset(2026, 10, 26, 12, 0, 1, TimeSpan.Zero));
        File.Exists(Path.Combine(rig.Done, receipt.Id + ".json")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("invalid-json", SteeringAckReason.InvalidJson)]
    [InlineData("invalid-schema", SteeringAckReason.InvalidSchema)]
    [InlineData("too-large", SteeringAckReason.TooLarge)]
    [InlineData("bad-id", SteeringAckReason.BadId)]
    [InlineData("duplicate", SteeringAckReason.Duplicate)]
    [InlineData("blank-text", SteeringAckReason.BlankText)]
    [InlineData("deliver-error", SteeringAckReason.DeliverError)]
    [InlineData("a-code-from-the-future", SteeringAckReason.Unknown)]
    public async Task ARejectedDirectiveCarriesItsReason(string code, SteeringAckReason expected)
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        rig.Respond(_ => ("rejected", code));

        var ack = await channel.WaitForAckAsync(receipt.Id, Dead, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var rejected = ack.ShouldNotBeNull();
        rejected.Status.ShouldBe(SteeringAckStatus.Rejected);
        rejected.Reason.ShouldBe(expected);
    }

    [Fact]
    public async Task AFailedDeliveryIsReportedAsFailedAndAnUnknownIdleStaysNull()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        rig.WriteAck(receipt.Id, "failed", "deliver-error", mode: null, idle: null);

        var ack = await channel.WaitForAckAsync(receipt.Id, TimeSpan.Zero, TestContext.Current.CancellationToken).ConfigureAwait(true);

        var failed = ack.ShouldNotBeNull();
        failed.Status.ShouldBe(SteeringAckStatus.Failed);
        failed.Reason.ShouldBe(SteeringAckReason.DeliverError);
        failed.Mode.ShouldBeNull();
        failed.Idle.ShouldBeNull();
    }

    [Fact]
    public async Task WithoutAnAcknowledgementTheWaitEndsWithNullWhenTheTimeoutHasPassed()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        var wait = channel.WaitForAckAsync(receipt.Id, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await rig.Clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);
        wait.IsCompleted.ShouldBeFalse();
        rig.Clock.Advance(TimeSpan.FromSeconds(5));

        (await wait.WaitAsync(Dead, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task AnAcknowledgementThatArrivesWhileWaitingEndsTheWaitAtTheNextPoll()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var wait = channel.WaitForAckAsync(receipt.Id, Dead, TestContext.Current.CancellationToken);
        await rig.Clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);

        rig.Respond();
        rig.Clock.Advance(FileSteeringChannelOptions.DefaultAckPollInterval);

        var ack = await wait.WaitAsync(Dead, TestContext.Current.CancellationToken).ConfigureAwait(true);
        ack.ShouldNotBeNull().Status.ShouldBe(SteeringAckStatus.Delivered);
    }

    [Fact]
    public async Task APartialInvalidForeignOrUnknownStatusAcknowledgementIsIgnoredUntilARealOneArrives()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var path = Path.Combine(rig.Acks, receipt.Id + ".json");
        var wait = channel.WaitForAckAsync(receipt.Id, Dead, TestContext.Current.CancellationToken);
        var timers = 1;
        await rig.Clock.WaitForTimersCreatedAsync(timers).ConfigureAwait(true);

        var unusable = new[]
        {
            "{\"v\":1,\"id\":\"" + receipt.Id + "\",\"stat",
            string.Empty,
            "[1,2,3]",
            "{\"v\":2,\"id\":\"" + receipt.Id + "\",\"status\":\"delivered\"}",
            "{\"v\":1,\"id\":\"000099-ffffffffff\",\"status\":\"delivered\"}",
            "{\"v\":1,\"id\":\"" + receipt.Id + "\",\"status\":\"maybe\"}",
        };
        foreach (var content in unusable)
        {
            await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken).ConfigureAwait(true);
            rig.Clock.Advance(FileSteeringChannelOptions.DefaultAckPollInterval);
            timers++;
            await rig.Clock.WaitForTimersCreatedAsync(timers).ConfigureAwait(true);
            wait.IsCompleted.ShouldBeFalse();
        }

        rig.WriteAck(receipt.Id, "delivered");
        rig.Clock.Advance(FileSteeringChannelOptions.DefaultAckPollInterval);

        (await wait.WaitAsync(Dead, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldNotBeNull().Status.ShouldBe(SteeringAckStatus.Delivered);
    }

    [Fact]
    public async Task ADirectiveIsRefusedWhenTheHeartbeatIsOlderThanFifteenSecondsAndNothingIsWritten()
    {
        using var rig = new SteerRig();
        rig.StartPlugin(TimeSpan.FromSeconds(15) + TimeSpan.FromMilliseconds(1));

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.NotServing);
        rig.InboxNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task AHeartbeatExactlyFifteenSecondsOldIsStillServingAndOneThatAgesIsRefusedLater()
    {
        using var rig = new SteerRig();
        rig.StartPlugin(TimeSpan.FromSeconds(15));
        var channel = rig.Channel();

        await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => channel.SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);
        failure.Reason.ShouldBe(SteeringFailureReason.NotServing);
        rig.InboxNames().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AFreshHeartbeatAfterAStaleOneRestoresSending()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        rig.Clock.Advance(TimeSpan.FromSeconds(60));
        await Should.ThrowAsync<SteeringUnavailableException>(() => channel.SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        rig.WriteAlive(TimeSpan.FromSeconds(5));
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        rig.InboxNames().ShouldBe([receipt.Id + ".json"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{\"v\":2,\"lastSeen\":\"2026-10-26T12:00:00.000Z\"}")]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"v\":1,\"lastSeen\":\"yesterday\"}")]
    [InlineData("{\"v\":1,\"lastSeen\":\"2026-10-26T12:10:00.000Z\"}")]
    public async Task AMissingUnreadableOrImplausibleHeartbeatMeansNotServing(string heartbeat)
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        rig.WriteAliveRaw(heartbeat);

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.NotServing);
        rig.InboxNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task ADirectoryWithoutAHeartbeatFileMeansNotServing()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        File.Delete(Path.Combine(rig.SteerDirectory, "alive.json"));

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.NotServing);
    }

    [Fact]
    public async Task ASessionWithoutASteerDirectoryIsRefusedAndNothingIsCreated()
    {
        using var rig = new SteerRig();

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.NoSteerDirectory);
        Directory.Exists(rig.SessionDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task AFreshHeartbeatWithoutAnInboxMeansNotServing()
    {
        using var rig = new SteerRig();
        rig.WriteAlive(TimeSpan.Zero);

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.NotServing);
        Directory.Exists(rig.Inbox).ShouldBeFalse();
    }

    [Fact]
    public async Task TheSixtyFifthWaitingDirectiveIsRefusedAsQueueFullAndTemporaryFilesDoNotCount()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        await File.WriteAllTextAsync(Path.Combine(rig.Inbox, "000000-aaaaaaaaaa.json.tmp"), "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var channel = rig.Channel();
        for (var i = 0; i < FileSteeringChannel.MaxQueuedDirectives; i++)
        {
            await channel.SendAsync(Directive("n" + i), TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => channel.SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.QueueFull);
        rig.InboxNames().Count.ShouldBe(FileSteeringChannel.MaxQueuedDirectives + 1);

        rig.Respond();
        var again = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        IdPattern().IsMatch(again.Id).ShouldBeTrue();
    }

    public static TheoryData<string> InvalidTexts() => new()
    {
        string.Empty,
        "   ",
        "\t\r\n",
        new string('x', FileSteeringChannel.MaxTextBytes + 1),
        new string('\u20ac', (FileSteeringChannel.MaxTextBytes / 3) + 1),
    };

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public async Task ABlankOrTooLongTextIsRefusedBeforeAnythingIsWritten(string text)
    {
        using var rig = new SteerRig();
        rig.StartPlugin();

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(text), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.InvalidDirective);
        rig.InboxNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnknownModeIsRefused()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(mode: (SteeringDirectiveMode)42), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.InvalidDirective);
    }

    [Fact]
    public async Task ATextOfExactly8192BytesIsAcceptedAndNonAsciiTextIsWrittenLiterallyAndRoundTrips()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var ascii = new string('a', FileSteeringChannel.MaxTextBytes);
        var euros = new string('\u20ac', (FileSteeringChannel.MaxTextBytes / 3) - 1);
        Encoding.UTF8.GetByteCount(euros).ShouldBeLessThanOrEqualTo(FileSteeringChannel.MaxTextBytes);
        var channel = rig.Channel();

        var first = await channel.SendAsync(Directive(ascii), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var second = await channel.SendAsync(Directive(euros + "\U0001F600"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        foreach (var id in new[] { first.Id, second.Id })
        {
            new FileInfo(Path.Combine(rig.Inbox, id + ".json")).Length.ShouldBeLessThanOrEqualTo(FileSteeringChannel.MaxFileBytes);
        }

        using var document = rig.ReadDirective(second.Id);
        document.RootElement.GetProperty("text").GetString().ShouldBe(euros + "\U0001F600");
        (await File.ReadAllTextAsync(Path.Combine(rig.Inbox, second.Id + ".json"), TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldContain("\u20ac", Case.Sensitive);
    }

    [Fact]
    public async Task ATextThatGrowsPastTheFileLimitWhenEscapedIsRefusedAsInvalid()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var quotes = new string('"', FileSteeringChannel.MaxTextBytes);

        var failure = await Should.ThrowAsync<SteeringUnavailableException>(
            () => rig.Channel().SendAsync(Directive(quotes), TestContext.Current.CancellationToken)).ConfigureAwait(true);

        failure.Reason.ShouldBe(SteeringFailureReason.InvalidDirective);
        rig.InboxNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectivesAreQueuedAndAnsweredInTheOrderTheyWereSent()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var sent = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            var receipt = await channel.SendAsync(Directive("message " + i), TestContext.Current.CancellationToken).ConfigureAwait(true);
            sent.Add(receipt.Id);
        }

        sent.ShouldBe(sent.Order(StringComparer.Ordinal).ToList());
        sent.Distinct(StringComparer.Ordinal).Count().ShouldBe(12);
        var texts = new List<string>();
        rig.Respond(text =>
        {
            texts.Add(text);
            return ("delivered", null);
        }).ShouldBe(sent);
        texts.ShouldBe(Enumerable.Range(0, 12).Select(i => "message " + i));
    }

    [Fact]
    public async Task TwoChannelsOverTheSameSessionNeverReuseAnIdentifier()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();

        var first = await rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var second = await rig.Channel().SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        second.Id.ShouldNotBe(first.Id, StringComparer.Ordinal);
        rig.InboxNames().Count.ShouldBe(2);
    }

    [Fact]
    public async Task ACancelledSendWritesNothingAndACancelledWaitThrowsCancellation()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => channel.SendAsync(Directive(), cancelled.Token)).ConfigureAwait(true);
        rig.InboxNames().ShouldBeEmpty();

        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => channel.WaitForAckAsync(receipt.Id, Dead, cancelled.Token)).ConfigureAwait(true);
    }

    [Fact]
    public async Task CancellingAWaitInProgressEndsItWithCancellation()
    {
        using var rig = new SteerRig();
        rig.StartPlugin();
        var channel = rig.Channel();
        var receipt = await channel.SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var wait = channel.WaitForAckAsync(receipt.Id, Dead, source.Token);
        await rig.Clock.WaitForTimersCreatedAsync(1).ConfigureAwait(true);

        await source.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(wait).ConfigureAwait(true);
    }

    [Fact]
    public async Task NoMessageDescriptionOrExceptionEverContainsTheTextOfADirectiveOrAPath()
    {
        using var rig = new SteerRig();
        var channel = rig.Channel();
        var messages = new List<string>();

        // No steering directory.
        messages.Add((await Should.ThrowAsync<SteeringUnavailableException>(() => channel.SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true)).Message);
        // Stale heartbeat.
        rig.StartPlugin(TimeSpan.FromMinutes(5));
        messages.Add((await Should.ThrowAsync<SteeringUnavailableException>(() => channel.SendAsync(Directive(), TestContext.Current.CancellationToken)).ConfigureAwait(true)).Message);
        // Invalid directive built from the secret.
        messages.Add((await Should.ThrowAsync<SteeringUnavailableException>(() => channel.SendAsync(Directive(Secret + new string('x', FileSteeringChannel.MaxTextBytes)), TestContext.Current.CancellationToken)).ConfigureAwait(true)).Message);
        // Full inbox.
        rig.WriteAlive(TimeSpan.Zero);
        for (var i = 0; i < FileSteeringChannel.MaxQueuedDirectives; i++)
        {
            await channel.SendAsync(Directive(Secret), TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        messages.Add((await Should.ThrowAsync<SteeringUnavailableException>(() => channel.SendAsync(Directive(Secret), TestContext.Current.CancellationToken)).ConfigureAwait(true)).Message);
        // Descriptions.
        var receipt = rig.Respond()[0];
        var ack = await channel.WaitForAckAsync(receipt, TimeSpan.Zero, TestContext.Current.CancellationToken).ConfigureAwait(true);
        messages.Add(Directive().ToString());
        messages.Add(ack.ShouldNotBeNull().ToString());
        messages.Add(new SteeringReceipt(receipt, SteeringDirectiveMode.Steer, rig.Clock.GetUtcNow()).ToString());

        messages.Count.ShouldBe(7);
        foreach (var message in messages)
        {
            message.ShouldNotBeNullOrWhiteSpace();
            message.ShouldNotContain("launch-code", Case.Sensitive);
            message.ShouldNotContain(rig.Root, Case.Sensitive);
        }
    }

    [Fact]
    public void ADirectiveDescriptionNamesTheModeAndTheLengthOnly()
    {
        Directive(Secret, SteeringDirectiveMode.FollowUp).ToString().ShouldBe("SteeringDirective { Mode = FollowUp, Length = " + Secret.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " }");
    }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a.b")]
    [InlineData("a b")]
    public async Task AnUnsafeIdentifierIsRejectedWhenWaitingForAnAcknowledgement(string id)
    {
        using var rig = new SteerRig();

        await Should.ThrowAsync<ArgumentException>(() => rig.Channel().WaitForAckAsync(id, Dead, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnIdentifierOfSixtyFiveCharactersIsRejectedAndSixtyFourIsAccepted()
    {
        using var rig = new SteerRig();
        var channel = rig.Channel();

        await Should.ThrowAsync<ArgumentException>(() => channel.WaitForAckAsync(new string('a', 65), TimeSpan.Zero, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        (await channel.WaitForAckAsync(new string('a', 64), TimeSpan.Zero, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ANegativeTimeoutIsRejected()
    {
        using var rig = new SteerRig();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => rig.Channel().WaitForAckAsync("abc", TimeSpan.FromSeconds(-1), TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public void TheChannelRejectsMissingArgumentsAndNonPositiveIntervals()
    {
        using var rig = new SteerRig();

        Should.Throw<ArgumentException>(() => new FileSteeringChannel(" ", rig.Clock));
        Should.Throw<ArgumentNullException>(() => new FileSteeringChannel(rig.SessionDirectory, null!));
        Should.Throw<ArgumentOutOfRangeException>(() => rig.Channel(new FileSteeringChannelOptions { AckPollInterval = TimeSpan.Zero }));
        Should.Throw<ArgumentOutOfRangeException>(() => rig.Channel(new FileSteeringChannelOptions { HeartbeatFreshness = TimeSpan.Zero }));
    }

    [Fact]
    public async Task ANullDirectiveIsAnArgumentErrorNotAnUnavailableSession()
    {
        using var rig = new SteerRig();

        await Should.ThrowAsync<ArgumentNullException>(() => rig.Channel().SendAsync(null!, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheHeartbeatFreshnessIsConfigurable()
    {
        using var rig = new SteerRig();
        rig.StartPlugin(TimeSpan.FromSeconds(20));

        var receipt = await rig.Channel(new FileSteeringChannelOptions { HeartbeatFreshness = TimeSpan.FromSeconds(30) })
            .SendAsync(Directive(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        receipt.Id.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TheDefaultsAreTheDocumentedValues()
    {
        FileSteeringChannelOptions.DefaultHeartbeatFreshness.ShouldBe(TimeSpan.FromSeconds(15));
        FileSteeringChannel.MaxQueuedDirectives.ShouldBe(64);
        FileSteeringChannel.MaxTextBytes.ShouldBe(8192);
        FileSteeringChannel.MaxFileBytes.ShouldBe(16 * 1024);
    }
}
