using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the bounded, append-only hook run log.
/// </summary>
public sealed class HookRunLogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HookRunRecord Record(string hookId, string? output = null) =>
        new(
            new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero),
            "conv-1",
            "gen-1",
            "preToolUse",
            hookId,
            "cursor-project:abc",
            HookScope.Project,
            "Shell",
            HookRunStatus.Succeeded,
            "allow",
            0,
            TimeSpan.FromMilliseconds(12.5),
            "hello",
            output,
            null,
            false,
            "abcdef",
            null);

    [Fact]
    public async Task RecordsAreKeptInMemoryNewestLastAndBoundedByTheCapacity()
    {
        using var log = new HookRunLog(null, memoryCapacity: 3);

        for (var index = 0; index < 5; index++)
        {
            await log.AppendAsync(Record($"h{index}"), Token);
        }

        log.Recent(10).Select(record => record.HookId).ShouldBe(["h2", "h3", "h4"], ignoreOrder: false);
        log.Recent(2).Select(record => record.HookId).ShouldBe(["h3", "h4"], ignoreOrder: false);
    }

    [Fact]
    public async Task EachRecordIsOneJsonLineWithoutAnyPayloadBody()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("hooks/runs.jsonl");
        using var log = new HookRunLog(path);

        await log.AppendAsync(Record("h1", "out"), Token);
        await log.AppendAsync(Record("h2"), Token);

        var lines = await File.ReadAllLinesAsync(path, Token);
        lines.Length.ShouldBe(2);
        using var first = JsonDocument.Parse(lines[0]);
        first.RootElement.GetProperty("hookId").GetString().ShouldBe("h1");
        first.RootElement.GetProperty("event").GetString().ShouldBe("preToolUse");
        first.RootElement.GetProperty("status").GetString().ShouldBe("Succeeded");
        first.RootElement.GetProperty("scope").GetString().ShouldBe("Project");
        first.RootElement.GetProperty("durationMs").GetDouble().ShouldBe(12.5);
        first.RootElement.GetProperty("payloadSha256").GetString().ShouldBe("abcdef");
        first.RootElement.GetProperty("standardOutput").GetString().ShouldBe("out");
        first.RootElement.TryGetProperty("payload", out _).ShouldBeFalse();
        first.RootElement.TryGetProperty("note", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task TheTwoFilesTogetherNeverExceedTheBound()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("runs.jsonl");
        const long maxBytes = 8 * 1024;
        using var log = new HookRunLog(path, maxBytes);
        var padding = new string('p', 700);

        for (var index = 0; index < 60; index++)
        {
            await log.AppendAsync(Record($"h{index}", padding), Token);
        }

        var current = new FileInfo(path).Length;
        var previous = new FileInfo(path + ".1").Length;
        (current + previous).ShouldBeLessThanOrEqualTo(maxBytes);
        previous.ShouldBeGreaterThan(0);
        File.Exists(path + ".2").ShouldBeFalse();
        var newest = (await File.ReadAllLinesAsync(path, Token)).Last();
        newest.ShouldContain("\"hookId\":\"h59\"", Case.Sensitive);
    }

    [Fact]
    public async Task AnExistingFileIsAppendedToAcrossInstances()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("runs.jsonl");
        using (var first = new HookRunLog(path))
        {
            await first.AppendAsync(Record("a"), Token);
        }

        using (var second = new HookRunLog(path))
        {
            await second.AppendAsync(Record("b"), Token);
        }

        (await File.ReadAllLinesAsync(path, Token)).Length.ShouldBe(2);
    }

    [Fact]
    public async Task AWriteThatFailsIsCountedAndNeverThrown()
    {
        using var directory = new TempDirectory();
        var blocker = directory.PathOf("file-not-directory");
        await File.WriteAllTextAsync(blocker, "x", Token);
        using var log = new HookRunLog(Path.Combine(blocker, "runs.jsonl"));

        await log.AppendAsync(Record("h"), Token);

        log.WriteFailures.ShouldBe(1);
        log.Recent(1).Single().HookId.ShouldBe("h");
    }

    [Fact]
    public void ExcerptsAreCutAtACharacterBoundary()
    {
        HookRunLog.Excerpt(null).ShouldBeNull();
        HookRunLog.Excerpt(string.Empty).ShouldBeNull();
        HookRunLog.Excerpt("short").ShouldBe("short");
        var long2Byte = string.Concat(Enumerable.Repeat("é", HookRunLog.ExcerptBytes));

        var cut = HookRunLog.Excerpt(long2Byte)!;

        Encoding.UTF8.GetByteCount(cut).ShouldBeLessThanOrEqualTo(HookRunLog.ExcerptBytes);
        cut.ShouldAllBe(character => character == 'é');
        cut.Length.ShouldBe(HookRunLog.ExcerptBytes / 2);
    }

    [Fact]
    public void TheBoundsAreValidated()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new HookRunLog(null, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => new HookRunLog(null, 1024, 0));
    }
}
