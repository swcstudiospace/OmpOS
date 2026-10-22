using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Rpc.Tests;

/// <summary>
/// Opt-in live capture of raw <c>omp</c> RPC frames (OD-07, Phase 11). Runs at most <see cref="MaxLiveTurns"/> trivial
/// prompt turns against the installed <c>omp</c>, writes every stdout frame line under <c>TestData/frames</c>, and
/// records one stop reason per turn in a sidecar <c>rpc-live-&lt;timestamp&gt;.turns.json</c>. Skipped unless
/// <c>OMPOS_RPC_LIVE_CAPTURE=1</c> and an <c>omp</c> executable is on PATH.
/// </summary>
[Trait("Category", "Live")]
public sealed class RpcLiveCaptureTests
{
    /// <summary>The owner-approved cap on prompt turns in one live capture run (OD-07).</summary>
    public const int MaxLiveTurns = 10;

    private const string CaptureVariable = "OMPOS_RPC_LIVE_CAPTURE";
    private const string Prompt = "Reply with the single word ok.";
    private const string TimeoutReason = "timeout";
    private const string MissingCompletionReason = "no_completion";
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(120);
    // Bound for omp's ready frame: the same 300 s bound RpcLiveSmokeTests.HandshakeTimeout uses.
    // Measured omp startup to ready is about 15 CPU-seconds, 16 to 19 s wall at load 6 to 14,
    // 40 to 58 s at load 36 to 43, and more than 120 s at load 56 (RpcLiveSmokeTests.cs:18-21).
    // The former 60 s bound failed under full parallel load.
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(300);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Gets whether the capture is requested and an <c>omp</c> executable is on PATH. Gates the capture.</summary>
    public static bool CaptureEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(CaptureVariable), "1", StringComparison.Ordinal)
        && RpcLiveSmokeTests.OmpOnPath;

    /// <summary>
    /// Runs up to <see cref="MaxLiveTurns"/> prompt turns, stopping early only when a turn exceeds 120 seconds. Every
    /// stdout frame line is written to <c>TestData/frames/rpc-live-&lt;UTC timestamp&gt;.ndjson</c>, and each turn's stop
    /// reason is written to the matching <c>.turns.json</c> sidecar.
    /// </summary>
    [Fact(SkipUnless = nameof(CaptureEnabled), Skip = "Set OMPOS_RPC_LIVE_CAPTURE=1 and put omp on PATH to run the live capture.")]
    public async Task LiveRpcCaptureRecordsBoundedTurns()
    {
        var ct = TestContext.Current.CancellationToken;
        string framesDirectory = Path.Combine(RepoRoot(), "tests", "OmpOS.Harness.Rpc.Tests", "TestData", "frames");
        Directory.CreateDirectory(framesDirectory);
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string framesPath = Path.Combine(framesDirectory, $"rpc-live-{stamp}.ndjson");
        string turnsPath = Path.Combine(framesDirectory, $"rpc-live-{stamp}.turns.json");
        string workdir = Path.Combine(Path.GetTempPath(), $"ompos-rpc-live-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workdir);

        var turns = new List<TurnRecord>();
        try
        {
            using StreamWriter frames = OpenFrames(framesPath);
            await RunCaptureAsync(frames, workdir, turns, ct).ConfigureAwait(true);
        }
        finally
        {
            await File.WriteAllTextAsync(turnsPath, TurnsJson(turns), ct).ConfigureAwait(true);
            Directory.Delete(workdir, recursive: true);
        }

        turns.Count.ShouldBeGreaterThanOrEqualTo(1);
        turns.Count.ShouldBeLessThanOrEqualTo(MaxLiveTurns);
        turns.ShouldAllBe(turn => turn.StopReason.Length > 0);
        new FileInfo(framesPath).Length.ShouldBeGreaterThan(0L);
    }

    private static async Task RunCaptureAsync(TextWriter frames, string workdir, List<TurnRecord> turns, CancellationToken ct)
    {
        OmpRpcProcess process = OmpRpcProcess.Start("omp", workdir);
        var transport = new FrameRecordingTransport(process, frames);
        var session = new OmpRpcSession(transport, HandshakeTimeout, CommandTimeout);
        try
        {
            await session.CreateAsync(ct).ConfigureAwait(true);

            for (int turn = 1; turn <= MaxLiveTurns; turn++)
            {
                string stopReason = await RunTurnAsync(session, ct).ConfigureAwait(true);
                turns.Add(new TurnRecord(turn, stopReason));
                if (string.Equals(stopReason, TimeoutReason, StringComparison.Ordinal))
                {
                    break;
                }
            }
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(true);
            await transport.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Submits one prompt and returns its stop reason: the <see cref="CompletedEvent"/> reason, or
    /// <c>timeout</c> when the turn did not end within <see cref="TurnTimeout"/>.
    /// </summary>
    private static async Task<string> RunTurnAsync(OmpRpcSession session, CancellationToken ct)
    {
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        turnCts.CancelAfter(TurnTimeout);
        string? stopReason = null;
        try
        {
            await foreach (HarnessEvent harnessEvent in session.SubmitPromptAsync(Prompt, new PromptOptions(), turnCts.Token).ConfigureAwait(true))
            {
                if (harnessEvent is CompletedEvent completed)
                {
                    stopReason = completed.Reason;
                }
            }
        }
        catch (OperationCanceledException) when (turnCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return TimeoutReason;
        }

        return stopReason ?? MissingCompletionReason;
    }

    private static StreamWriter OpenFrames(string path) =>
        new(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

    private static string TurnsJson(IEnumerable<TurnRecord> turns)
    {
        var array = new JsonArray();
        foreach (TurnRecord turn in turns)
        {
            array.Add(new JsonObject
            {
                ["turn"] = turn.Turn,
                ["stopReason"] = turn.StopReason,
            });
        }

        return array.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("OmpOS.slnx was not found above the test output directory.");
    }

    /// <summary>The stop reason of one prompt turn, in submission order.</summary>
    private sealed record TurnRecord(int Turn, string StopReason);

    /// <summary>
    /// Forwards every transport call to the real <see cref="OmpRpcProcess"/> and copies each stdout frame line, exactly as
    /// received, to the capture file before the session sees it.
    /// </summary>
    private sealed class FrameRecordingTransport(IRpcTransport inner, TextWriter frames) : IRpcTransport
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (string line in inner.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
            {
                await frames.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
                yield return line;
            }
        }

        /// <inheritdoc />
        public Task SendLineAsync(string line, CancellationToken cancellationToken) =>
            inner.SendLineAsync(line, cancellationToken);

        /// <inheritdoc />
        public Task CloseInputAsync(CancellationToken cancellationToken) => inner.CloseInputAsync(cancellationToken);

        /// <inheritdoc />
        public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => inner.WaitForExitAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
