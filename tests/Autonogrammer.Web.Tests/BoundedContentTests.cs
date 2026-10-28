using System.Text;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>Bounded body reads: an oversized body fails naming its limit; a body within the limit is returned unchanged.</summary>
public sealed class BoundedContentTests
{
    /// <summary>A declared Content-Length above the limit is refused, naming the limit.</summary>
    [Fact]
    public async Task OversizedContentLengthThrowsNamingTheLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes("[]   "));

        var ex = await Should.ThrowAsync<ApiClientException>(() => BoundedContent.ReadTextAsync(content, 4, ct));

        ex.Message.ShouldContain("4-byte limit");
    }

    /// <summary>A streamed body without a declared length is refused once it passes the limit, naming the limit.</summary>
    [Fact]
    public async Task OversizedStreamedBodyThrowsNamingTheLimit()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new ChunkedSseContent(["0123456789"]);

        var ex = await Should.ThrowAsync<ApiClientException>(() => BoundedContent.ReadTextAsync(content, 5, ct));

        ex.Message.ShouldContain("5-byte limit");
    }

    /// <summary>A body within the limit is returned unchanged, including one of exactly the limit.</summary>
    [Fact]
    public async Task NormalBodyIsReturnedUnchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var text = new ByteArrayContent(Encoding.UTF8.GetBytes("hello"));
        (await BoundedContent.ReadTextAsync(text, 5, ct)).ShouldBe("hello");

        using var json = new ByteArrayContent(Encoding.UTF8.GetBytes("[]"));
        var sessions = await BoundedContent.ReadJsonAsync(json, AutonogrammerJsonContext.Default.SessionArray, 2, ct);
        sessions.ShouldNotBeNull().ShouldBeEmpty();
    }
}
