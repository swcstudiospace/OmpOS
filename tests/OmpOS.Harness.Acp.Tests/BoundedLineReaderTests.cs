using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Acp.Tests;

/// <summary>Bounded line reads: lines come back without their terminator, and a line past the 1 MiB cap is refused.</summary>
public sealed class BoundedLineReaderTests
{
    /// <summary>Lines end at LF or CRLF, an empty line is an empty string, and the end of the stream is null.</summary>
    [Fact]
    public async Task ReturnsLinesWithoutTerminatorsThenNullAtEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        using var reader = new StringReader("first\nsecond\r\n\nlast");

        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBe("first");
        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBe("second");
        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBe(string.Empty);
        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBe("last");
        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A line of exactly the cap is returned unchanged.</summary>
    [Fact]
    public async Task LineAtTheLimitIsReturnedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var line = new string('a', BoundedLineReader.MaxLineLength);
        using var reader = new StringReader(line + "\n");

        (await BoundedLineReader.ReadLineAsync(reader, ct).ConfigureAwait(true)).ShouldBe(line);
    }

    /// <summary>A line one character past the cap is refused as a malformed frame.</summary>
    [Fact]
    public async Task LineOverTheLimitIsRefusedAsMalformed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var reader = new StringReader(new string('a', BoundedLineReader.MaxLineLength + 1) + "\n");

        var malformed = await Should.ThrowAsync<MalformedFrameHarnessException>(
            () => BoundedLineReader.ReadLineAsync(reader, ct)).ConfigureAwait(true);

        malformed.Message.ShouldBe("A physical frame exceeds the 1 MiB transport limit.");
    }
}
