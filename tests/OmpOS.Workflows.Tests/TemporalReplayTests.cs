using System.Diagnostics.CodeAnalysis;
using OmpOS.Workflows.Worker;
using Temporalio.Api.History.V1;
using Temporalio.Common;
using Temporalio.Worker;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Temporal workflow replay tests for determinism verification.
/// </summary>
/// <remarks>
/// Replays <see cref="TemporalSessionRunWorkflow"/> over the committed history fixture
/// with the shared source-generated <see cref="SessionRunDataConverter"/> (reflection
/// serialization is disabled in this repo). The history JSON is parsed with the protobuf
/// parser (unaffected by the STJ reflection switch); the fixture itself is exported from
/// a real dev-server run (see the ADR). Replay must not throw for deterministic workflow code.
/// </remarks>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class TemporalReplayTests
{
    private const string HistoryPath = "TestData/histories/session-run.json";

    /// <summary>
    /// Verifies the session run workflow replays deterministically against the recorded history.
    /// </summary>
    [Fact]
    public async Task SessionRunWorkflow_ReplaysDeterministically()
    {
        Assert.True(File.Exists(HistoryPath), $"History fixture not found at {HistoryPath}.");
        var history = await LoadHistoryAsync();
        Assert.NotEmpty(history.Events);

        var replayer = new WorkflowReplayer(
            new WorkflowReplayerOptions { DataConverter = SessionRunDataConverter.Instance }
                .AddWorkflow<TemporalSessionRunWorkflow>());

        // Act & Assert — must not throw for deterministic replay.
        await replayer.ReplayWorkflowAsync(history, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies the replay fixture parses to a non-empty workflow history.
    /// </summary>
    [Fact]
    public void HistoryFixture_IsWellFormed()
    {
        Assert.True(File.Exists(HistoryPath), $"History fixture not found at {HistoryPath}.");

        var json = File.ReadAllText(HistoryPath);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var history = ParseHistory(json);
        Assert.NotEmpty(history.Events);
    }

    private static async Task<WorkflowHistory> LoadHistoryAsync()
    {
        var json = await File.ReadAllTextAsync(HistoryPath, TestContext.Current.CancellationToken);
        return ParseHistory(json);
    }

    private static WorkflowHistory ParseHistory(string json)
    {
        // The protobuf parser (not STJ) reads the exported history, so fixture parsing
        // is unaffected by the repo-wide reflection-serialization switch.
        var proto = History.Parser.ParseJson(json);
        return new WorkflowHistory("session-run-001", proto.Events);
    }
}
