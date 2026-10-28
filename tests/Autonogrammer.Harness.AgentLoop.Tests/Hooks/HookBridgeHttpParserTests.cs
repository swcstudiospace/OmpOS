using System.Globalization;
using System.Text;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies the strict request-head parser: what it reads, and the shapes it refuses because a lenient reader could take them
/// two ways (obsolete folding, repeated length headers, bare line feeds, control bytes, a request line that is not exactly
/// <c>METHOD SP target SP HTTP/1.1</c>).
/// </summary>
public sealed class HookBridgeHttpParserTests
{
    private const string Line = "POST /v1/hook HTTP/1.1\r\n";

    private static int Parse(string head, out HookBridgeHttpHead? parsed) =>
        HookBridgeHttpParser.Parse(Encoding.Latin1.GetBytes(head + "\r\n"), out parsed);

    [Fact]
    public void AValidHeadYieldsMethodTargetAuthorizationAndLength()
    {
        var status = Parse(Line + "Host: 127.0.0.1\r\nAuthorization: Bearer abc\r\nContent-Length: 42\r\nContent-Type: application/json\r\n", out var head);

        status.ShouldBe(200);
        head.ShouldNotBeNull();
        head.Method.ShouldBe("POST");
        head.Target.ShouldBe("/v1/hook");
        head.Authorization.ShouldBe("Bearer abc");
        head.ContentLength.ShouldBe(42);
        head.HasTransferEncoding.ShouldBeFalse();
    }

    [Fact]
    public void HeaderNamesAreMatchedWithoutRegardToCaseAndValuesAreTrimmed()
    {
        var status = Parse(Line + "CONTENT-length:\t 7 \t\r\nauthorization:   Bearer   x  \r\nTRANSFER-ENCODING: chunked\r\n", out var head);

        status.ShouldBe(200);
        head.ShouldNotBeNull();
        head.ContentLength.ShouldBe(7);
        head.Authorization.ShouldBe("Bearer   x");
        head.HasTransferEncoding.ShouldBeTrue();
    }

    [Fact]
    public void AHeadWithoutHeadersIsValidAndHasNoLengthOrAuthorization()
    {
        var status = Parse("GET /x HTTP/1.1\r\n", out var head);

        status.ShouldBe(200);
        head.ShouldNotBeNull();
        head.Method.ShouldBe("GET");
        head.Target.ShouldBe("/x");
        head.Authorization.ShouldBeNull();
        head.ContentLength.ShouldBeNull();
    }

    [Fact]
    public void AContentLengthTooLargeForALongReadsAsTheMaximum()
    {
        var status = Parse(Line + "Content-Length: 99999999999999999999999\r\n", out var head);

        status.ShouldBe(200);
        head.ShouldNotBeNull();
        head.ContentLength.ShouldBe(long.MaxValue);
    }

    [Theory]
    [InlineData("Content-Length: 5\r\nContent-Length: 5\r\n")]
    [InlineData("Content-Length: 5\r\ncontent-length: 6\r\n")]
    [InlineData("Authorization: Bearer a\r\nAuthorization: Bearer b\r\n")]
    [InlineData("Content-Length: \r\n")]
    [InlineData("Content-Length: -1\r\n")]
    [InlineData("Content-Length: +1\r\n")]
    [InlineData("Content-Length: 1 2\r\n")]
    [InlineData("Content-Length: 0x10\r\n")]
    [InlineData("Content-Length: 5,5\r\n")]
    [InlineData("Content-Length: 1.5\r\n")]
    public void AmbiguousOrMalformedLengthAndAuthorizationHeadersAreRefused(string headers)
    {
        Parse(Line + headers, out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Theory]
    [InlineData("Host: a\r\n folded\r\n")]
    [InlineData("Host: a\r\n\tfolded\r\n")]
    [InlineData(" Host: a\r\n")]
    [InlineData("Host : a\r\n")]
    [InlineData("Host\t: a\r\n")]
    [InlineData("Host a\r\n")]
    [InlineData(": a\r\n")]
    [InlineData("Ho st: a\r\n")]
    [InlineData("Ho(st: a\r\n")]
    [InlineData("Host: a\nX: b\r\n")]
    [InlineData("Host: a\rX: b\r\n")]
    [InlineData("Host: a\u007fb\r\n")]
    [InlineData("Host: a\u00e9b\r\n")]
    [InlineData("X-\u00e9: a\r\n")]
    public void ShapesThatCouldBeReadTwoWaysAreRefused(string headers)
    {
        Parse(Line + headers, out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Fact]
    public void ANulByteInAHeaderValueIsRefused()
    {
        Parse(Line + "Host: a" + (char)0 + "b\r\n", out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Theory]
    [InlineData("POST /v1/hook HTTP/1.0\r\n")]
    [InlineData("POST /v1/hook HTTP/2\r\n")]
    [InlineData("POST /v1/hook http/1.1\r\n")]
    [InlineData("POST /v1/hook\r\n")]
    [InlineData("POST\r\n")]
    [InlineData("\r\n")]
    [InlineData("POST  /v1/hook HTTP/1.1\r\n")]
    [InlineData("POST /v1/hook  HTTP/1.1\r\n")]
    [InlineData("POST /v1/hook HTTP/1.1 \r\n")]
    [InlineData(" POST /v1/hook HTTP/1.1\r\n")]
    [InlineData("POST v1/hook HTTP/1.1\r\n")]
    [InlineData("POST http://127.0.0.1:1/v1/hook HTTP/1.1\r\n")]
    [InlineData("POST * HTTP/1.1\r\n")]
    [InlineData("POST /v1/hook#frag HTTP/1.1\r\n")]
    [InlineData("PO\tST /v1/hook HTTP/1.1\r\n")]
    [InlineData("P(OST /v1/hook HTTP/1.1\r\n")]
    [InlineData("POST /v1/\thook HTTP/1.1\r\n")]
    [InlineData("POSTPOSTPOSTPOSTPOST /v1/hook HTTP/1.1\r\n")]
    public void ARequestLineThatIsNotExactlyMethodTargetAndHttp11IsRefused(string requestLine)
    {
        Parse(requestLine + "Host: a\r\n", out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Fact]
    public void ABareLineFeedInTheRequestLineIsRefused()
    {
        Parse("POST /v1/hook HTTP/1.1\nHost: a\r\n", out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Fact]
    public void AHeadWithoutItsBlankLineIsRefused()
    {
        HookBridgeHttpParser.Parse("POST /v1/hook HTTP/1.1\r\nHost: a\r\n"u8, out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Fact]
    public void ABlankLineInsideTheHeadIsRefused()
    {
        var bytes = "POST /v1/hook HTTP/1.1\r\nHost: a\r\n\r\nX: b\r\n\r\n"u8;

        HookBridgeHttpParser.Parse(bytes, out var head).ShouldBe(400);

        head.ShouldBeNull();
    }

    [Fact]
    public void ExactlyTheMaximumNumberOfHeaderLinesIsAcceptedAndOneMoreIsTooLarge()
    {
        var atLimit = string.Concat(Enumerable.Range(0, HookBridgeHttpParser.MaxHeaderLines).Select(static index => "X-" + index.ToString(CultureInfo.InvariantCulture) + ": v\r\n"));
        var overLimit = atLimit + "X-Extra: v\r\n";

        Parse(Line + atLimit, out var accepted).ShouldBe(200);
        Parse(Line + overLimit, out var refused).ShouldBe(431);

        accepted.ShouldNotBeNull();
        refused.ShouldBeNull();
    }
}
