using System.Text;
using OmpOS.AgentWorker.Bridge;
using Shouldly;
using Xunit;

namespace OmpOS.AgentWorker.Tests;

/// <summary>Which text frames may enter omp's one-object-per-line stdio protocol, and how they are rewritten.</summary>
public sealed class AcpFrameGuardTests
{
    /// <summary>A compact object passes through unchanged.</summary>
    [Fact]
    public void CompactObjectIsUnchanged()
    {
        AcpFrameGuard.TryNormalize(Encoding.UTF8.GetBytes("{\"id\":1}"), out var frame).ShouldBeTrue();

        Encoding.UTF8.GetString(frame).ShouldBe("{\"id\":1}");
    }

    /// <summary>Line breaks between tokens are removed; the same characters inside a string stay escaped.</summary>
    [Fact]
    public void LineBreaksBetweenTokensAreRemovedAndEscapedOnesInStringsStay()
    {
        AcpFrameGuard.TryNormalize(Encoding.UTF8.GetBytes("{\r\n\"a\":\"x\\ny\",\n\"b\":[1,\r\n2]\n}"), out var frame).ShouldBeTrue();

        Encoding.UTF8.GetString(frame).ShouldBe("{\"a\":\"x\\ny\",\"b\":[1,2]}");
    }

    /// <summary>Anything that is not exactly one JSON object is refused.</summary>
    /// <param name="text">The frame text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("{")]
    [InlineData("{\"a\":1}x")]
    [InlineData("{\"a\":1}\n{\"b\":2}")]
    public void NonObjectsAreRefused(string text)
    {
        AcpFrameGuard.TryNormalize(Encoding.UTF8.GetBytes(text), out var frame).ShouldBeFalse();

        frame.ShouldBeEmpty();
    }

    /// <summary>Invalid UTF-8 inside a string is refused rather than forwarded.</summary>
    [Fact]
    public void InvalidUtf8IsRefused()
    {
        byte[] bytes = [(byte)'{', (byte)'"', (byte)'a', (byte)'"', (byte)':', (byte)'"', 0xFF, (byte)'"', (byte)'}'];

        AcpFrameGuard.TryNormalize(bytes, out _).ShouldBeFalse();
    }
}
