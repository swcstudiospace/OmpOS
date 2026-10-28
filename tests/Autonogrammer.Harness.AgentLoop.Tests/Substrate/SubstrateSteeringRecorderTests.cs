using System.Net;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>The request body, the never-send-text rule and the Recorded, Skipped and Failed outcomes of <see cref="SubstrateSteeringRecorder"/>.</summary>
public sealed class SubstrateSteeringRecorderTests
{
    private const string StoredReply = """{"id":"01JNEW","ts":"2026-10-26T08:11:00.000Z","hash":"hh","stored":true}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SteeringRecord RecordFor(string text, string mode = "steer") =>
        new()
        {
            Mode = mode,
            Length = text.Length,
            TextSha256Prefix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12],
            At = new DateTimeOffset(2026, 10, 26, 8, 11, 0, TimeSpan.Zero),
        };

    private static async Task<(SteeringRecordResult Result, GatewayRig Rig)> RecordAsync(Func<RecordedRequest, HttpResponseMessage> respond, StageObservationScope scope, SteeringRecord record)
    {
        var rig = GatewayRig.Create(respond);
        var recorder = new SubstrateSteeringRecorder(rig.Gateway, rig.Logger);
        var result = await recorder.RecordAsync(scope, record, Ct).ConfigureAwait(true);
        return (result, rig);
    }

    [Fact]
    public async Task EmitsExactlyTheDocumentedNoteEventAndReportsRecorded()
    {
        const string Text = "please use the shared presentation layer";
        var record = RecordFor(Text);

        var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, StoredReply), SubstrateTestSupport.Scope(), record).ConfigureAwait(true);

        using (rig)
        {
            result.ShouldBe(new SteeringRecordResult(SteeringRecordOutcome.Recorded, null));
            var request = rig.Requests.ShouldHaveSingleItem();
            request.Method.ShouldBe(HttpMethod.Post);
            request.Uri.AbsolutePath.ShouldBe("/events");
            request.Authorization.ShouldBe("Bearer " + SubstrateTestSupport.Token);
            request.Body.ShouldBe(
                "{\"kind\":\"note\",\"summary\":\"operator steering\",\"session_id\":\"acp-session-1\",\"graph_id\":\"ut-lq1a2b3c-0f1e2d3c\",\"surface\":\"omp\",\"actor\":\"human\",\"payload\":{\"action\":\"steer\",\"item\":\""
                + record.TextSha256Prefix
                + "\",\"outcome\":\"queued\",\"mode\":\"steer\",\"length\":"
                + Text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "}}");
        }
    }

    [Fact]
    public async Task TheOperatorTextIsNeverSentOnlyItsLengthAndHashPrefix()
    {
        const string Text = "SECRET-OPERATOR-TEXT: rotate the production keys";
        var record = RecordFor(Text, mode: "follow-up");

        var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, StoredReply), SubstrateTestSupport.Scope(), record).ConfigureAwait(true);

        using (rig)
        {
            result.Outcome.ShouldBe(SteeringRecordOutcome.Recorded);
            var body = rig.Requests.Single().Body;
            body.ShouldNotContain("SECRET", Case.Sensitive);
            body.ShouldNotContain("rotate", Case.Sensitive);
            body.ShouldNotContain("production", Case.Sensitive);
            body.ShouldContain("\"item\":\"" + record.TextSha256Prefix + "\"");
            body.ShouldContain("\"mode\":\"follow-up\"");
            body.ShouldContain("\"length\":" + Text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            record.TextSha256Prefix.Length.ShouldBe(12);
            Text.ShouldNotContain(record.TextSha256Prefix, Case.Sensitive);
        }
    }

    [Fact]
    public async Task WithoutAnAcpSessionOrGraphTheOmpSessionIdIsUsedAndNoGraphIdIsSent()
    {
        var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, StoredReply), SubstrateTestSupport.Scope(graphId: null, acpSessionId: null), RecordFor("x")).ConfigureAwait(true);

        using (rig)
        {
            result.Outcome.ShouldBe(SteeringRecordOutcome.Recorded);
            var body = rig.Requests.Single().Body;
            body.ShouldContain("\"session_id\":\"session-1\"");
            body.ShouldNotContain("graph_id", Case.Sensitive);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task AnUnavailableServerIsSkippedWithAReasonAndNoException(HttpStatusCode status)
    {
        var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(status, "{\"error\":\"GREPTIME_URL is not set\"}"), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);

        using (rig)
        {
            result.Outcome.ShouldBe(SteeringRecordOutcome.Skipped);
            result.Reason.ShouldNotBeNull().ShouldContain("not available");
            result.Reason.ShouldNotContain("GREPTIME", Case.Sensitive);
        }
    }

    [Fact]
    public async Task AConnectionFailureAndATimeoutAreSkipped()
    {
        var (refused, refusedRig) = await RecordAsync(_ => throw new HttpRequestException("refused"), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);
        var (slow, slowRig) = await RecordAsync(_ => throw new TaskCanceledException("slow"), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);

        using (refusedRig)
        using (slowRig)
        {
            refused.Outcome.ShouldBe(SteeringRecordOutcome.Skipped);
            slow.Outcome.ShouldBe(SteeringRecordOutcome.Skipped);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ARejectedRequestFailsWithoutLeakingTheTokenOrTheBody(HttpStatusCode status)
    {
        var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(status, "Bearer " + SubstrateTestSupport.Token + " BODY-MARKER"), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);

        using (rig)
        {
            result.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
            result.Reason.ShouldNotBeNull().ShouldNotContain(SubstrateTestSupport.Token, Case.Sensitive);
            result.Reason.ShouldNotContain("BODY-MARKER", Case.Sensitive);
            rig.Logger.Lines.ShouldAllBe(static line => !line.Contains(SubstrateTestSupport.Token, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task AReplyThatSaysTheEventWasNotStoredIsAFailureAndAnUnreadableReplyIsToo()
    {
        var (notStored, notStoredRig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, """{"stored":false,"error":"ledger down"}"""), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);
        var (garbage, garbageRig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, "<html>oops</html>"), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);

        using (notStoredRig)
        using (garbageRig)
        {
            notStored.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
            notStored.Reason.ShouldNotBeNull().ShouldNotContain("ledger down", Case.Sensitive);
            garbage.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
        }
    }

    [Fact]
    public async Task AnInvalidRecordFailsWithoutSendingAnything()
    {
        var good = RecordFor("x");
        var bad = new[]
        {
            good with { Mode = "shout" },
            good with { Length = -1 },
            good with { TextSha256Prefix = "NOTHEX" },
            good with { TextSha256Prefix = "0123456789abcdef" },
            good with { TextSha256Prefix = "0123456789AB" },
        };

        foreach (var record in bad)
        {
            var (result, rig) = await RecordAsync(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, StoredReply), SubstrateTestSupport.Scope(), record).ConfigureAwait(true);

            using (rig)
            {
                result.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
                rig.Requests.ShouldBeEmpty();
            }
        }
    }

    [Fact]
    public async Task ACancelledRecordingReportsFailedInsteadOfThrowing()
    {
        using var rig = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.Accepted, StoredReply));
        var recorder = new SubstrateSteeringRecorder(rig.Gateway);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(true);

        var result = await recorder.RecordAsync(SubstrateTestSupport.Scope(), RecordFor("x"), cts.Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
        result.Reason.ShouldNotBeNull().ShouldContain("cancelled");
    }

    [Fact]
    public async Task AnUnexpectedFaultInTheTransportStillNeverEscapesTheRecorder()
    {
        var (result, rig) = await RecordAsync(_ => throw new InvalidOperationException("boom " + SubstrateTestSupport.Token), SubstrateTestSupport.Scope(), RecordFor("x")).ConfigureAwait(true);

        using (rig)
        {
            result.Outcome.ShouldBe(SteeringRecordOutcome.Failed);
            result.Reason.ShouldNotBeNull().ShouldNotContain(SubstrateTestSupport.Token, Case.Sensitive);
        }
    }
}
