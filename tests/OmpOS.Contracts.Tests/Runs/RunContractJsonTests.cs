using System.Text.Json;
using OmpOS.Contracts.Runs;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Runs;

/// <summary>
/// Verifies the hosted run shapes added in contract 1.2.0: they travel through the source-generated context under camel-case names,
/// the status travels by name, and an unfinished run's output carries its status and nothing else.
/// </summary>
public sealed class RunContractJsonTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    /// <summary>An unfinished run reports its status and no output: the wire has no transcript and no changes.</summary>
    [Fact]
    public void AnUnfinishedRunOutputWritesItsStatusAndNothingElse()
    {
        var output = new RunOutput { RunId = "r1", Status = RunStatus.Running };

        var json = JsonSerializer.Serialize(output, OmpOsJsonContext.Default.RunOutput);

        json.ShouldBe("""{"runId":"r1","status":"Running"}""");
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.RunOutput).ShouldNotBeNull();
        back.Transcript.ShouldBeNull();
        back.Changes.ShouldBeNull();
    }

    /// <summary>A settled run's output keeps its transcript in order and every file change, including the absent old text of a created file.</summary>
    [Fact]
    public void ASettledRunOutputRoundTripsItsTranscriptAndChanges()
    {
        var output = new RunOutput
        {
            RunId = "r1",
            Status = RunStatus.Completed,
            Transcript = new RunTranscript
            {
                Messages = [new Message { Role = "user", Content = "add a file", Timestamp = Created }],
                Events =
                [
                    new StreamEvent { Seq = 1, SessionId = "s1", CorrelationId = "s1/turn-1", Timestamp = Created, Payload = new TextDeltaPayload { Delta = "done" } },
                    new StreamEvent { Seq = 2, SessionId = "s1", CorrelationId = "s1/turn-1", Timestamp = Created, Payload = new CompletedPayload { Reason = "end_turn" } },
                ],
            },
            Changes =
            [
                new RunFileChange { Path = "src/new.cs", NewText = "class A;" },
                new RunFileChange { Path = "src/old.cs", OldText = "class B;", NewText = "class B { }" },
            ],
        };

        var json = JsonSerializer.Serialize(output, OmpOsJsonContext.Default.RunOutput);
        var back = JsonSerializer.Deserialize(json, OmpOsJsonContext.Default.RunOutput).ShouldNotBeNull();

        back.Status.ShouldBe(RunStatus.Completed);
        var transcript = back.Transcript.ShouldNotBeNull();
        transcript.Messages.ShouldHaveSingleItem().Content.ShouldBe("add a file");
        transcript.Events.Select(static evt => evt.Seq).ShouldBe([1L, 2L]);
        transcript.Events[0].Payload.ShouldBeOfType<TextDeltaPayload>().Delta.ShouldBe("done");
        transcript.Events[1].Payload.ShouldBeOfType<CompletedPayload>().Reason.ShouldBe("end_turn");
        var changes = back.Changes.ShouldNotBeNull();
        changes.Count.ShouldBe(2);
        changes[0].OldText.ShouldBeNull();
        changes[0].NewText.ShouldBe("class A;");
        changes[1].OldText.ShouldBe("class B;");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("changes")[0].TryGetProperty("oldText", out _).ShouldBeFalse();
    }

    /// <summary>The run carries camel-case names, a string status, and omits the completion time until the run settles.</summary>
    [Theory]
    [InlineData(RunStatus.Queued, "Queued")]
    [InlineData(RunStatus.Running, "Running")]
    [InlineData(RunStatus.Completed, "Completed")]
    [InlineData(RunStatus.Failed, "Failed")]
    [InlineData(RunStatus.Cancelled, "Cancelled")]
    public void ARunWritesItsStatusByNameAndOmitsAnUnsetCompletionTime(RunStatus status, string wireName)
    {
        var run = new Run { Id = "r1", SessionId = "s1", Status = status, CreatedAt = Created };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(run, OmpOsJsonContext.Default.Run));

        var root = document.RootElement;
        root.GetProperty("id").GetString().ShouldBe("r1");
        root.GetProperty("sessionId").GetString().ShouldBe("s1");
        root.GetProperty("status").GetString().ShouldBe(wireName);
        root.TryGetProperty("completedAt", out _).ShouldBeFalse();
    }
}
