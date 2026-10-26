using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>SSE parser length limits: an oversized line or frame fails with an error that names the limit.</summary>
public sealed class SseParserLimitTests
{
    /// <summary>A partial line that grows past the line limit throws before it is buffered.</summary>
    [Fact]
    public void OversizedPartialLineThrowsNamingTheLimit()
    {
        var parser = new SseParser();

        var ex = Should.Throw<ApiClientException>(() => parser.Feed("data: " + new string('x', SseParser.MaxLineLength)));

        ex.Message.ShouldContain("line");
        ex.Message.ShouldContain($"{SseParser.MaxLineLength}");
    }

    /// <summary>A completed line longer than the line limit throws even when its newline arrives in the same chunk.</summary>
    [Fact]
    public void OversizedTerminatedLineThrows()
    {
        var parser = new SseParser();
        var line = "data: " + new string('x', SseParser.MaxLineLength) + "\n";

        Should.Throw<ApiClientException>(() => parser.Feed(line));
    }

    /// <summary>Data lines that each fit the line limit still throw once the frame's data exceeds the frame limit.</summary>
    [Fact]
    public void OversizedFrameDataThrowsNamingTheLimit()
    {
        var parser = new SseParser();
        var dataLine = "data: " + new string('x', 512 * 1024) + "\n";

        var ex = Should.Throw<ApiClientException>(() =>
        {
            for (var i = 0; i < 9; i++)
            {
                parser.Feed(dataLine);
            }
        });

        ex.Message.ShouldContain("frame");
        ex.Message.ShouldContain($"{SseParser.MaxFrameLength}");
    }

    /// <summary>A dispatched frame resets the frame length counter, so the next frame is measured on its own.</summary>
    [Fact]
    public void DispatchResetsTheFrameLengthCounter()
    {
        var parser = new SseParser();
        var dataLine = "data: " + new string('x', SseParser.MaxLineLength - 6) + "\n";
        var frame = dataLine + dataLine + dataLine + "\n";

        parser.Feed(frame).Count.ShouldBe(1);
        parser.Feed(frame).Count.ShouldBe(1);
    }
}
